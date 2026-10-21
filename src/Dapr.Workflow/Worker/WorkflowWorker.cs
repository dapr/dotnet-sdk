// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker.Grpc;
using Dapr.Workflow.Worker.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker;

/// <summary>
/// Background service that processes workflow and activity work items from the Dapr sidecar.
/// </summary>
internal sealed class WorkflowWorker(
    TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient,
    IWorkflowsFactory workflowsFactory,
    ILoggerFactory loggerFactory,
    IDaprSerializer workflowSerializer,
    IServiceProvider serviceProvider,
    IConfiguration? configuration = null,
    WorkflowRuntimeOptions? options = null) : BackgroundService
{
    private readonly TaskHubSidecarService.TaskHubSidecarServiceClient _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
    private readonly IWorkflowsFactory _workflowsFactory = workflowsFactory ?? throw new ArgumentNullException(nameof(workflowsFactory));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<WorkflowWorker> _logger = loggerFactory.CreateLogger<WorkflowWorker>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly IDaprSerializer _serializer = workflowSerializer ?? throw new ArgumentNullException(nameof(workflowSerializer));
    private readonly WorkflowRuntimeOptions _options = options ?? new WorkflowRuntimeOptions();
    private readonly string? _daprApiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
    
    private GrpcProtocolHandler? _protocolHandler;
    private CancellationToken _stoppingToken;

    /// <summary>
    /// Executes the workflow worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        _logger.LogWorkerWorkflowStart();

        try
        {
            // Create the protocol handler
            _protocolHandler = new GrpcProtocolHandler(_grpcClient, loggerFactory, _daprApiToken,
                _options.DisableStatefulHistory, _options.HistoryCacheTtl,
                _options.HistoryCacheMaxInstances, _options.HistoryCacheMaxBytes);

            // Start processing work items
            await _protocolHandler.StartAsync(HandleWorkflowResponseAsync, HandleActivityResponseAsync, stoppingToken);
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

    private async Task<WorkflowResponse> HandleWorkflowResponseAsync(WorkflowRequest request, string completionToken)
    {
        _logger.LogWorkerWorkflowHandleOrchestratorRequestStart(request.InstanceId);

        WorkflowTrace.TraceActivityScope traceScope = default;
        try
        {
            // Create a scope for DI
            await using var scope = _serviceProvider.CreateAsyncScope();

            // The protocol handler resolves all transport-backed history before invoking user code.
            var allPastEvents = request.PastEvents.ToList();

            // Extract the following values from the ExecutionStartedEvent in the history
            string? workflowName = null;
            string? serializedInput = null;
            string? appId = null;

            // TaskExecutionId derivation needs a per-execution seed. Older
            // sidecars leave ExecutionId unset on the request, so recover the
            // runtime-minted execution ID persisted in the
            // ExecutionStartedEvent: it is stable across replays of one
            // execution and fresh for each execution of a recreated instance
            // ID. Without it, two executions of the same instance ID derive
            // colliding TaskExecutionIds, and the runtime's activity
            // de-duplication can treat the second execution's activity as a
            // duplicate of the first, leaving the workflow stuck in Running.
            // It seeds ONLY the TaskExecutionId namespace (see the context
            // constructor): the NewGuid / child-instance-ID namespace keeps
            // its historical seeding so in-flight executions replay
            // identically across an SDK upgrade.
            string? historyExecutionId = null;

            // Termination is terminal even when completions follow it in the same batch.
            var timelineEvents = allPastEvents.Concat(request.NewEvents).ToList();
            
            // Restore the trace context provided by the sidecar so Activity.Current is non-null
            traceScope = WorkflowTrace.StartOrchestrationTrace(timelineEvents);
            
            if (timelineEvents.Any(e => e.ExecutionTerminated is not null))
            {
                return new WorkflowResponse
                {
                    InstanceId = request.InstanceId,
                    CompletionToken = completionToken,
                    Actions =
                    {
                        new WorkflowAction
                        {
                            CompleteWorkflow = new CompleteWorkflowAction
                            {
                                WorkflowStatus = OrchestrationStatus.Terminated
                            }
                        }
                    }
                };
            }

            // Suspended turns only commit history. Replay on resume will deliver buffered
            // completions in order, without requiring workflow activation while paused.
            if (timelineEvents.LastOrDefault(e => e.ExecutionSuspended is not null ||
                                                 e.ExecutionResumed is not null)?.ExecutionSuspended is not null)
            {
                return new WorkflowResponse { InstanceId = request.InstanceId, CompletionToken = completionToken };
            }

            // Create a new version tracker for this turn
            var versionTracker = new WorkflowVersionTracker(allPastEvents);

            var routerRegistry = scope.ServiceProvider.GetService<IWorkflowRouterRegistry>();
            var resolvedFromRouter = false;

            // Identify the workflow name from the now-complete history
            // Process in reverse so this finds the most recent event instead of processing
            // such an event handled by another app.
            foreach (var e in allPastEvents.Concat(request.NewEvents).Reverse())
            {
                if (e.ExecutionStarted != null)
                {
                    workflowName = e.ExecutionStarted.Name;
                    serializedInput = e.ExecutionStarted.Input;

                    historyExecutionId = e.ExecutionStarted.WorkflowInstance?.ExecutionId;

                    // Try pulling the app ID out of the target first, then the source if not available
                    if (!string.IsNullOrEmpty(e.Router?.TargetAppID))
                    {
                        appId = e.Router.TargetAppID;
                    }
                    else if (!string.IsNullOrEmpty(e.Router?.SourceAppID))
                    {
                        appId = e.Router.SourceAppID;
                    }
                    break;
                }
            }

            // if (string.IsNullOrEmpty(workflowName))
            // {
            //     foreach (var e in allPastEvents.Concat(request.NewEvents).Reverse())
            //     {
            //         var state = e.
            //     }
            // }

            if (string.IsNullOrEmpty(workflowName))
            {
                foreach (var e in allPastEvents.Concat(request.NewEvents).Reverse())
                {
                    var versionName = e.WorkflowStarted?.Version?.Name;
                    if (!string.IsNullOrEmpty(versionName))
                    {
                        workflowName = versionName;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(workflowName))
            {
                if (routerRegistry is not null && routerRegistry.TryResolveLatest(out workflowName))
                {
                    resolvedFromRouter = true;
                }
                else
                {
                    _logger.LogWorkerWorkflowHandleOrchestratorRequestNotInRegistry("<unknown>");
                    return new WorkflowResponse
                    {
                        InstanceId = request.InstanceId,
                        CompletionToken = completionToken,
                        Actions =
                        {
                            new WorkflowAction
                            {
                                CompleteWorkflow = new CompleteWorkflowAction
                                {
                                    WorkflowStatus = OrchestrationStatus.Failed,
                                    FailureDetails = new()
                                    {
                                        IsNonRetriable = true,
                                        ErrorType = "WorkflowNameMissing",
                                        ErrorMessage = "Workflow name missing and no routable workflow could be resolved.",
                                        StackTrace = string.Empty
                                    }
                                }
                            }
                        }
                    };
                }
            }

            if (routerRegistry is not null && !routerRegistry.Contains(workflowName))
            {
                _logger.LogWorkerWorkflowHandleOrchestratorRequestNotInRegistry(workflowName);
                return new WorkflowResponse
                {
                    InstanceId = request.InstanceId,
                    CompletionToken = completionToken,
                    Actions =
                    {
                        new WorkflowAction
                        {
                            CompleteWorkflow = new CompleteWorkflowAction
                            {
                                WorkflowStatus = OrchestrationStatus.Failed,
                                FailureDetails = new()
                                {
                                    IsNonRetriable = true,
                                    ErrorType = "WorkflowNotFound",
                                    ErrorMessage = $"Workflow '{workflowName}' is not registered in this app.",
                                    StackTrace = string.Empty
                                }
                            }
                        }
                    }
                };
            }

            // Try to get the workflow from the factory
            var workflowIdentifier = new TaskIdentifier(workflowName);
            if (!_workflowsFactory.TryCreateWorkflow(workflowIdentifier, scope.ServiceProvider, out var workflow, out var workflowActivationException))
            {
                if (workflowActivationException != null)
                {
                    _logger.LogWorkerWorkflowHandleOrchestratorRequestActivationFailed(workflowActivationException, workflowName);
                    
                    return new WorkflowResponse
                    {
                        InstanceId = request.InstanceId,
                        CompletionToken = completionToken,
                        Actions =
                        {
                            new WorkflowAction
                            {
                                CompleteWorkflow = new CompleteWorkflowAction
                                {
                                    WorkflowStatus = OrchestrationStatus.Failed,
                                    FailureDetails = new()
                                    {
                                        IsNonRetriable = true,
                                        ErrorType = workflowActivationException.GetType().FullName ?? "WorkflowActivationFailed",
                                        ErrorMessage = $"Workflow '{workflowName}' failed to activate: {workflowActivationException.Message}",
                                        StackTrace = workflowActivationException.StackTrace ?? string.Empty
                                    }
                                }
                            }
                        }
                    };
                }

                _logger.LogWorkerWorkflowHandleOrchestratorRequestNotInRegistry(workflowName);

                return new WorkflowResponse
                {
                    InstanceId = request.InstanceId,
                    CompletionToken = completionToken,
                    Actions =
                    {
                        new WorkflowAction
                        {
                            CompleteWorkflow = new CompleteWorkflowAction
                            {
                                WorkflowStatus = OrchestrationStatus.Failed,
                                FailureDetails = new()
                                {
                                    IsNonRetriable = true,
                                    ErrorType = "WorkflowNotFound",
                                    ErrorMessage = $"Workflow '{workflowName}' is not registered in this app.",
                                    StackTrace = string.Empty
                                }
                            }
                        }
                    }
                };
            }

            var currentUtcDateTime = allPastEvents.Count > 0 && allPastEvents[0].Timestamp != null
                ? allPastEvents[0].Timestamp.ToDateTime()
                : DateTime.UtcNow;

            var currentTurnStartedEvent = request.NewEvents.Reverse()
                .FirstOrDefault(e => e.WorkflowStarted != null);

            var currentTurnTimestamp = currentTurnStartedEvent?.Timestamp?.ToDateTime()
                ?? currentUtcDateTime;

            // Initialize the context with the FULL history
            var incomingPropagatedHistory = request.PropagatedHistory?.Chunks.Count > 0
                ? request.PropagatedHistory.Chunks
                : null;
            var codec = scope.ServiceProvider.GetService<IWorkflowPayloadConverter>();
            var payloads = codec is null ? null : new WorkflowPayloadHandler(_serializer, codec, _stoppingToken);
            var isChild = timelineEvents.LastOrDefault(e => e.ExecutionStarted is not null)?.ExecutionStarted.ParentInstance is not null;
            var features = new WorkflowFeatureCollection();
            foreach (var provider in scope.ServiceProvider.GetServices<IWorkflowFeatureProvider>())
            {
                provider.Configure(request.InstanceId, features);
            }

            if (payloads is not null && features.Get<IWorkflowPayloadResolver>() is { } resolver)
            {
                features.Set<IWorkflowPayloadResolver>(new QueuedWorkflowPayloadResolver(resolver, payloads));
            }
            var context = new WorkflowOrchestrationContext(workflowName, request.InstanceId, currentUtcDateTime,
                _serializer, loggerFactory, versionTracker, appId, request.ExecutionId,
                allPastEvents,
                incomingPropagatedHistory,
                historyExecutionId, payloads, features);

            // Deserialize the input
            object? input = string.IsNullOrEmpty(serializedInput)
                ? null
                : payloads is null
                    ? _serializer.Deserialize(serializedInput, workflow!.InputType)
                    : await payloads.DecodeAsync(serializedInput, workflow!.InputType,
                        new(isChild ? PayloadKind.ChildWorkflowInput : PayloadKind.WorkflowInput, request.InstanceId, workflowName));

            // Initialize per-turn state before any workflow code runs.
            // On first execution (no past events) use the current turn's OrchestratorStarted timestamp.
            // On replay use the first past event's timestamp so the workflow sees the same
            // CurrentUtcDateTime at its start as it did on the very first execution.
            // ProcessEvents will advance the clock naturally via OrchestratorStarted history events.
            var initialTimestamp = allPastEvents.Count > 0 ? currentUtcDateTime : currentTurnTimestamp;
            context.InitializeNewTurn(initialTimestamp);
            context.SetReplayState(allPastEvents.Count > 0);

            // Execute the workflow
            // IMPORTANT: Orchestrations intentionally "block" on incomplete tasks (activities, timers, events)
            // during the first execution pass. We must NOT await indefinitely here; we need to return the pending actions.
            // We run the workflow BEFORE processing new events so that the code reaches the 'await' points and registers tasks
            // in _openTasks. Then, ProcessEvents(NewEvents) can satisfy those tasks.
            var runTask = workflow!.RunAsync(context, input);
            if (payloads is not null)
            {
                await payloads.DrainAsync().ConfigureAwait(false);
            }

            // Replay the old history to rebuild the local state of the orchestration.
            if (allPastEvents.Count > 0)
            {
                await context.ProcessEventsAsync(allPastEvents, true).ConfigureAwait(false);
            }

            // Play the newly arrived events to determine the next action to take.
            if (request.NewEvents.Count > 0)
            {
                await context.ProcessEventsAsync(request.NewEvents, false).ConfigureAwait(false);
            }

            if (context.IsSuspended)
            {
                return new WorkflowResponse { InstanceId = request.InstanceId, CompletionToken = completionToken };
            }

            // Populate CarryoverEvents now that all events in this turn have been processed.
            // ContinueAsNew cannot do this inline because it runs mid-ProcessEvents; events
            // arriving later in the same NewEvents batch would be buffered after the snapshot.
            context.FinalizeCarryoverEvents();

            // If the history processing caused a stall (e.g. via OnOrchestratorStarted), return immediately
            if (versionTracker.IsStalled)
            {
                return new WorkflowResponse
                {
                    InstanceId = request.InstanceId,
                    CompletionToken = completionToken,
                    Actions =
                    {
                        new WorkflowAction
                        {
                            CompleteWorkflow = new CompleteWorkflowAction
                            {
                                WorkflowStatus = OrchestrationStatus.Stalled,
                                Details = versionTracker.StalledEvent?.Description ??
                                          "Workflow stalled due to patch mismatch."
                            }
                        }
                    }
                };
            }
            
            // Get all pending actions from the context
            var response = new WorkflowResponse
            {
                InstanceId = request.InstanceId,
                CompletionToken = completionToken
            };
            
            // Stamp the version info if new patches were encountered this turn
            if (versionTracker.IncludeVersionInNextResponse || resolvedFromRouter)
            {
                response.Version = versionTracker.BuildResponseVersion(workflowName);
            }

            // Add all actions that were scheduled during workflow execution
            context.ValidatePendingCallInputs();
            response.Actions.AddRange(context.PendingActions);

            // Set custom status if provided
            if (context.CustomStatus != null)
                response.CustomStatus = payloads is null
                    ? _serializer.Serialize(context.CustomStatus)
                    : context.SerializedCustomStatus;

            // If the workflow issued ContinueAsNew, it already queued a completion action; just return it.
            if (context.PendingActions.Any(a => 
                    a.CompleteWorkflow?.WorkflowStatus == OrchestrationStatus.ContinuedAsNew))
            {
                _logger.LogWorkerWorkflowHandleOrchestratorRequestCompleted(workflowName, request.InstanceId);
                if (payloads is not null)
                    await payloads.EncodeResponseAsync(response, request.InstanceId, workflowName, isChild).ConfigureAwait(false);
                return response;
            }

            if (!runTask.IsCompleted)
            {
                _logger.LogWorkflowWorkerOrchestratorYield(request.InstanceId, response.Actions.Count, 
                    context.PendingActions.Count);

                if (response.Actions.Count == 0 && context.PendingActions.Count == 0) 
                    _logger.LogWorkflowWorkerOrchestratorStall(request.InstanceId);
                
                if (payloads is not null)
                    await payloads.EncodeResponseAsync(response, request.InstanceId, workflowName, isChild).ConfigureAwait(false);
                return response;
            }

            // If we are here, the workflow method has finished - we must handle the result or exception
            try
            {
                // The workflow completed synchronously (either on replay or it had nothing to await).
                // Observe exceptions if any, otherwise serialize the output and complete the orchestration.
                var output = await runTask.ConfigureAwait(false);
                var outputJson = payloads is null
                    ? output != null ? _serializer.Serialize(output) : string.Empty
                    : payloads.Snapshot(output);

                response.Actions.Add(new WorkflowAction
                {
                    CompleteWorkflow = new CompleteWorkflowAction
                    {
                        Result = outputJson,
                        WorkflowStatus = OrchestrationStatus.Completed
                    }
                });
            }
            catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WorkflowTrace.SetCurrentError(ex);
                
                // Report the failure as an action so Dapr records the workflow as FAILED
                response.Actions.Add(new WorkflowAction
                {
                    CompleteWorkflow = new CompleteWorkflowAction
                    {
                        WorkflowStatus = OrchestrationStatus.Failed,
                        FailureDetails = new()
                        {
                            IsNonRetriable = true,
                            ErrorType = ex.GetType().FullName ?? "Exception",
                            ErrorMessage = ex.Message,
                            StackTrace = ex.StackTrace ?? string.Empty
                        }
                    }
                });
            }

            _logger.LogWorkerWorkflowHandleOrchestratorRequestCompleted(workflowName, request.InstanceId);
            if (payloads is not null)
                await payloads.EncodeResponseAsync(response, request.InstanceId, workflowName, isChild).ConfigureAwait(false);
            return response;
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            WorkflowTrace.SetCurrentError(ex);
            _logger.LogWorkerWorkflowHandleOrchestratorRequestFailed(ex, request.InstanceId);

            return new WorkflowResponse
            {
                InstanceId = request.InstanceId,
                CompletionToken = completionToken,
                Actions =
                {
                    new WorkflowAction
                    {
                        CompleteWorkflow = new()
                        {
                            WorkflowStatus = OrchestrationStatus.Failed,
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
        finally
        {
            traceScope.Dispose();
        }
    }

    private async Task<ActivityResponse> HandleActivityResponseAsync(ActivityRequest request, string completionToken)
    {
        _logger.LogWorkerWorkflowHandleActivityRequestStart(request.Name, request.WorkflowInstance?.InstanceId, request.TaskId);

        try
        {
            // Create a scope for DI
            await using var scope = _serviceProvider.CreateAsyncScope();

            // Try to get the activity from the factory
            var activityIdentifier = new TaskIdentifier(request.Name);
            if (!_workflowsFactory.TryCreateActivity(activityIdentifier, scope.ServiceProvider, out var activity, out var activityActivationException))
            {
                if (activityActivationException != null)
                {
                    WorkflowTrace.SetCurrentError(activityActivationException);
                    _logger.LogWorkerWorkflowHandleActivityRequestActivationFailed(activityActivationException, request.Name);

                    return new ActivityResponse
                    {
                        InstanceId = request.WorkflowInstance?.InstanceId ?? string.Empty,
                        TaskId = request.TaskId,
                        CompletionToken = completionToken,
                        FailureDetails = new()
                        {
                            ErrorType = activityActivationException.GetType().FullName ?? "ActivityActivationFailed",
                            ErrorMessage = $"Activity '{request.Name}' failed to activate: {activityActivationException.Message}",
                            StackTrace = activityActivationException.StackTrace ?? string.Empty
                        }
                    };
                }

                _logger.LogWorkerWorkflowHandleActivityRequestNotInRegistry(request.Name);

                return new ActivityResponse
                {
                    InstanceId = request.WorkflowInstance?.InstanceId ?? string.Empty,
                    TaskId = request.TaskId,
                    CompletionToken = completionToken,
                    FailureDetails = new()
                    {
                        ErrorType = "ActivityNotFoundException",
                        ErrorMessage = $"Activity '{request.Name}' not found",
                        StackTrace = string.Empty
                    }
                };
            }

            // Create the activity context
            var taskExecutionKey = !string.IsNullOrEmpty(request.TaskExecutionId)
                ? request.TaskExecutionId
                : request.TaskId.ToString();

            var context = new WorkflowActivityContextImpl(activityIdentifier,
                request.WorkflowInstance?.InstanceId ?? string.Empty, taskExecutionKey, request.TaskId);
            var codec = scope.ServiceProvider.GetService<IWorkflowPayloadConverter>();
            var payloads = codec is null ? null : new WorkflowPayloadHandler(_serializer, codec, _stoppingToken);

            // Deserialize the input
            object? input = null;
            if (!string.IsNullOrEmpty(request.Input))
            {
                input = payloads is null
                    ? _serializer.Deserialize(request.Input, activity!.InputType)
                    : await payloads.DecodeAsync(request.Input, activity!.InputType,
                        new(PayloadKind.ActivityInput, context.InstanceId, request.Name)).ConfigureAwait(false);
            }

            // Execute the activity
            var output = await activity!.RunAsync(context, input);

            // Serialize output
            var outputJson = payloads is null
                ? output != null ? _serializer.Serialize(output) : string.Empty
                : await payloads.EncodeAsync(payloads.Snapshot(output),
                    new(PayloadKind.ActivityOutput, context.InstanceId, request.Name)).ConfigureAwait(false);

            _logger.LogWorkerWorkflowHandleActivityRequestCompleted(request.Name, request.TaskId);

            return new ActivityResponse
            {
                InstanceId = request.WorkflowInstance?.InstanceId ?? string.Empty,
                TaskId = request.TaskId,
                Result = outputJson,
                CompletionToken = completionToken
            };
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            WorkflowTrace.SetCurrentError(ex);
            _logger.LogWorkerWorkflowHandleActivityRequestFailed(ex, request.Name, request.WorkflowInstance?.InstanceId);

            return new ActivityResponse
            {
                InstanceId = request.WorkflowInstance?.InstanceId ?? string.Empty,
                TaskId = request.TaskId,
                CompletionToken = completionToken,
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
