// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Worker.Grpc;
using Dapr.Workflow.Worker.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker;

/// <summary>
/// Background service that processes workflow and activity work items from the Dapr sidecar.
/// </summary>
internal sealed class WorkflowWorker(TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient, IWorkflowsFactory workflowsFactory, ILoggerFactory loggerFactory, IWorkflowSerializer workflowSerializer, IServiceProvider serviceProvider, WorkflowRuntimeOptions options) : BackgroundService
{
    private readonly TaskHubSidecarService.TaskHubSidecarServiceClient _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
    private readonly IWorkflowsFactory _workflowsFactory = workflowsFactory ?? throw new ArgumentNullException(nameof(workflowsFactory));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<WorkflowWorker> _logger = loggerFactory?.CreateLogger<WorkflowWorker>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly WorkflowRuntimeOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    private GrpcProtocolHandler? _protocolHandler;

    /// <summary>
    /// Executes the workflow worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWorkerWorkflowStart();

        try
        {
            // Create the protocol handler
            _protocolHandler = new GrpcProtocolHandler(_grpcClient, loggerFactory, _options.MaxConcurrentWorkflows, _options.MaxConcurrentActivities);
            
            // Start processing work items
            await _protocolHandler.StartAsync(HandleOrchestratorResponseAsync, HandleActivityResponseAsync, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWorkerWorkflowCanceled();
        }
        catch (Exception ex)
        {
            _logger.LogWorkerWorkflowError(ex);
            throw;
        }
    }

    private async Task<OrchestratorResponse> HandleOrchestratorResponseAsync(OrchestratorRequest request)
    {
        _logger.LogWorkerWorkflowHandleOrchestratorRequestStart(request.InstanceId);

        try
        {
            // Create a scope for DI
            await using var scope = _serviceProvider.CreateAsyncScope();

            // Extract the workflow name from the ExecutionStartedEvent in the history
            string? workflowName = null;
            string? serializedInput = null;
            foreach (var historyEvent in request.PastEvents)
            {
                if (historyEvent.ExecutionStarted != null)
                {
                    workflowName = historyEvent.ExecutionStarted.Name;
                    serializedInput = historyEvent.ExecutionStarted.Input;
                    break;
                }
            }

            if (string.IsNullOrEmpty(workflowName))
            {
                _logger.LogWorkerWorkflowHandleOrchestratorRequestNotInRegistry("<unknown>");
                return new OrchestratorResponse { InstanceId = request.InstanceId, Actions = { }, CustomStatus = null };
            }

            // Try to get the workflow from the factory
            var workflowIdentifier = new TaskIdentifier(workflowName);
            if (!_workflowsFactory.TryCreateWorkflow(workflowIdentifier, scope.ServiceProvider, out var workflow))
            {
                _logger.LogWorkerWorkflowHandleOrchestratorRequestNotInRegistry(workflowName);

                return new OrchestratorResponse { InstanceId = request.InstanceId, Actions = { }, CustomStatus = null };
            }

            // Create the workflow context
            var currentUtcDateTime = DateTime.UtcNow;
            
            // Try to get a more accurate timestamp from the first history event
            if (request.PastEvents.Count > 0 && request.PastEvents[0].Timestamp != null)
                currentUtcDateTime = request.PastEvents[0].Timestamp.ToDateTime();
            
            var context = new WorkflowOrchestrationContext(workflowName, request.InstanceId, request.PastEvents, currentUtcDateTime, workflowSerializer, loggerFactory);

            // Deserialize the input
            object? input = null;
            if (!string.IsNullOrEmpty(serializedInput))
            {
                input = workflowSerializer.Deserialize(serializedInput, workflow!.InputType);
            }

            // Execute the workflow
            var output = await workflow!.RunAsync(context, input);
            
            // Get all pending actions from the context
            var response = new OrchestratorResponse { InstanceId = request.InstanceId };
            
            // Add all actions that were scheduled during workflow execution
            response.Actions.AddRange(context.PendingActions);
            
            // If the workflow completed without ContinueAsNew, add completion action
            if (context.PendingActions.All(a => a.CompleteOrchestration?.OrchestrationStatus != OrchestrationStatus.ContinuedAsNew))
            {
                // Serialize the output
                var outputJson = output != null ? workflowSerializer.Serialize(output) : string.Empty;
                
                response.Actions.Add(new OrchestratorAction
                {
                    CompleteOrchestration = new CompleteOrchestrationAction
                    {
                        Result = outputJson,
                        OrchestrationStatus = OrchestrationStatus.Completed
                    }
                });
            }
            
            // Set custom status if provided
            if (context.CustomStatus != null)
                response.CustomStatus = workflowSerializer.Serialize(context.CustomStatus);
            
            _logger.LogWorkerWorkflowHandleOrchestratorRequestCompleted(workflowName, request.InstanceId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWorkerWorkflowHandleOrchestratorRequestFailed(ex, request.InstanceId);

            return new OrchestratorResponse
            {
                InstanceId = request.InstanceId,
                Actions =
                {
                    new OrchestratorAction
                    {
                        CompleteOrchestration = new()
                        {
                            OrchestrationStatus = OrchestrationStatus.Failed,
                            FailureDetails = new()
                            {
                                ErrorType = ex.GetType().FullName ?? "Exception",
                                ErrorMessage = ex.Message,
                                StackTrace = ex.StackTrace ?? string.Empty
                            }
                        }
                    }
                }
            };
        }
    }

    private async Task<ActivityResponse> HandleActivityResponseAsync(ActivityRequest request)
    {
        _logger.LogWorkerWorkflowHandleActivityRequestStart(request.Name, request.OrchestrationInstance?.InstanceId, request.TaskId);

        try
        {
            // Create a scope for DI
            await using var scope = _serviceProvider.CreateAsyncScope();
            
            // Try to get the activity from the factory
            var activityIdentifier = new TaskIdentifier(request.Name);
            if (!_workflowsFactory.TryCreateActivity(activityIdentifier, scope.ServiceProvider, out var activity))
            {
                _logger.LogWorkerWorkflowHandleActivityRequestNotInRegistry(request.Name);

                return new ActivityResponse
                {
                    InstanceId = request.OrchestrationInstance?.InstanceId ?? string.Empty,
                    TaskId = request.TaskId,
                    FailureDetails = new()
                    {
                        ErrorType = "ActivityNotFoundException",
                        ErrorMessage = $"Activity '{request.Name}' not found",
                        StackTrace = string.Empty
                    }
                };
            }
            
            // Create the activity context
            var context = new WorkflowActivityContextImpl(activityIdentifier,
                request.OrchestrationInstance?.InstanceId ?? string.Empty);
            
            // Deserialize the input
            object? input = null;
            if (!string.IsNullOrEmpty(request.Input))
            {
                input = workflowSerializer.Deserialize(request.Input, activity!.InputType);
            }
            
            // Execute the activity
            var output = await activity!.RunAsync(context, input);
            
            // Serialize output
            var outputJson = output != null
                ? workflowSerializer.Serialize(output)
                : string.Empty;
            
            _logger.LogWorkerWorkflowHandleActivityRequestCompleted(request.Name, request.TaskId);

            return new ActivityResponse
            {
                InstanceId = request.OrchestrationInstance?.InstanceId ?? string.Empty,
                TaskId = request.TaskId,
                Result = outputJson
            };
        }
        catch(Exception ex)
        {
            _logger.LogWorkerWorkflowHandleActivityRequestFailed(ex, request.Name, request.OrchestrationInstance?.InstanceId);

            return new ActivityResponse
            {
                InstanceId = request.OrchestrationInstance?.InstanceId ?? string.Empty,
                TaskId = request.TaskId,
                FailureDetails = new()
                {
                    ErrorType = ex.GetType().FullName ?? "Exception",
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace ?? string.Empty
                }
            };
        }
    }

    /// <summary>
    /// Disposes resources when stopping.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogWorkerWorkflowStop();

        if (_protocolHandler != null)
            await _protocolHandler.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}
