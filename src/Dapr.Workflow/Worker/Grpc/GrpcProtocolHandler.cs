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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.DurableTask.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker.Grpc;

/// <summary>
/// Handles the bidirectional gRPC streaming protocol with the Dapr sidecar.
/// </summary>
internal sealed class GrpcProtocolHandler(TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient, ILogger<GrpcProtocolHandler> logger, int maxConcurrentWorkItems = 100, int maxConcurrentActivities = 100) : IAsyncDisposable
{
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly ILogger<GrpcProtocolHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly TaskHubSidecarService.TaskHubSidecarServiceClient _grpcClient =
        grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
    private readonly int _maxConcurrentWorkItems =  maxConcurrentWorkItems > 0 ? maxConcurrentWorkItems : throw new ArgumentOutOfRangeException(nameof(maxConcurrentWorkItems));
    private readonly int _maxConcurrentActivities = maxConcurrentActivities > 0 ? maxConcurrentActivities : throw new ArgumentOutOfRangeException(nameof(maxConcurrentActivities));

    private AsyncServerStreamingCall<WorkItem>? _streamingCall;
    private int _activeWorkItemCount;

    /// <summary>
    /// Starts the streaming connection with the Dapr sidecar.
    /// </summary>
    /// <param name="workflowHandler">Handler for workflow work items.</param>
    /// <param name="activityHandler">Handler for activity work items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(
        Func<OrchestratorRequest, Task<OrchestratorResponse>> workflowHandler,
        Func<ActivityRequest, Task<ActivityResponse>> activityHandler,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
        var token = linkedCts.Token;

        try
        {
            _logger.LogGrpcProtocolHandlerStartStream();

            // Establish the bidirectional stream
            var request = new GetWorkItemsRequest
            {
                MaxConcurrentOrchestrationWorkItems = _maxConcurrentWorkItems,
                MaxConcurrentActivityWorkItems = _maxConcurrentActivities
            };
            
            // Establish the server streaming call
            _streamingCall = _grpcClient.GetWorkItems(request, cancellationToken: token);
            
            // Process work items from the stream
            await ReceiveLoopAsync(_streamingCall.ResponseStream, workflowHandler, activityHandler, token);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogGrpcProtocolHandlerStreamCanceled();
        }
        catch (Exception ex)
        {
            _logger.LogGrpcProtocolHandlerGenericError(ex);
            throw;
        }
    }

    /// <summary>
    /// Receives requests from the Dapr sidecar and processes them.
    /// </summary>
    private async Task ReceiveLoopAsync(
        IAsyncStreamReader<WorkItem> workItemsStream,
        Func<OrchestratorRequest, Task<OrchestratorResponse>> orchestratorHandler,
        Func<ActivityRequest, Task<ActivityResponse>> activityHandler,
        CancellationToken cancellationToken)
    {
        // Track active work items for proper exception handling
        var activeWorkItems = new List<Task>();
        
        try
        {
            await foreach (var workItem in workItemsStream.ReadAllAsync(cancellationToken))
            {
                // Dispatch based on work item type
                var workItemTask = workItem.RequestCase switch
                {
                    WorkItem.RequestOneofCase.OrchestratorRequest => ProcessWorkflowAsync(workItem.OrchestratorRequest,
                        orchestratorHandler, cancellationToken),
                    WorkItem.RequestOneofCase.ActivityRequest => ProcessActivityAsync(workItem.ActivityRequest,
                        activityHandler, cancellationToken),
                    _ => Task.Run(
                        () => _logger.LogGrpcProtocolHandlerUnknownWorkItemType(workItem.RequestCase),
                        cancellationToken)
                };

                activeWorkItems.Add(workItemTask);

                // Clean up completed tasks periodically
                if (activeWorkItems.Count > _maxConcurrentWorkItems * 2)
                {
                    activeWorkItems.RemoveAll(t => t.IsCompleted);
                }
            }

            _logger.LogGrpcProtocolHandlerReceiveLoopCompleted(activeWorkItems.Count);
            
            // Wait for all active work items to complete
            if (activeWorkItems.Count > 0)
            {
                await Task.WhenAll(activeWorkItems);
            }
        }
        catch (Exception ex)
        {
            _logger.LogGrpcProtocolHandlerReceiveLoopError(ex);
            throw;
        }
    }
    
    /// <summary>
    /// Processes a workflow request work item.
    /// </summary>
    private async Task ProcessWorkflowAsync(OrchestratorRequest request,
        Func<OrchestratorRequest, Task<OrchestratorResponse>> handler, CancellationToken cancellationToken)
    {
        var activeCount = Interlocked.Increment(ref _activeWorkItemCount);

        try
        {
            _logger.LogGrpcProtocolHandlerWorkflowProcessorStart(request.InstanceId, activeCount);

            var result = await handler(request);
            
            // Send the result back to Dapr
            await _grpcClient.CompleteOrchestratorTaskAsync(result, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogGrpcProtocolHandlerWorkflowProcessorCanceled(request.InstanceId);
        }
        catch (Exception ex)
        {
            try
            {
                var failureResult = CreateWorkflowFailureResult(request, ex);
                await _grpcClient.CompleteOrchestratorTaskAsync(failureResult, cancellationToken: cancellationToken);
            }
            catch (Exception resultEx)
            {
                _logger.LogGrpcProtocolHandlerWorkflowProcessorFailedToSendError(resultEx, request.InstanceId);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeWorkItemCount);
        }
    }

    /// <summary>
    /// Processes an activity request work item.
    /// </summary>
    private async Task ProcessActivityAsync(ActivityRequest request,
        Func<ActivityRequest, Task<ActivityResponse>> handler, CancellationToken cancellationToken)
    {
        var activeCount = Interlocked.Increment(ref _activeWorkItemCount);

        try
        {
            _logger.LogGrpcProtocolHandlerActivityProcessorStart(request.OrchestrationInstance.InstanceId, request.Name,
                request.TaskId, activeCount);
            var result = await handler(request);

            // Send the result back to Dapr
            await _grpcClient.CompleteActivityTaskAsync(result, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogGrpcProtocolHandlerActivityProcessorCanceled(request.Name);
        }
        catch (Exception ex)
        {
            _logger.LogGrpcProtocolHandlerActivityProcessorError(ex, request.Name,
                request.OrchestrationInstance?.InstanceId);

            try
            {
                var failureResult = CreateActivityFailureResult(request, ex);
                await _grpcClient.CompleteActivityTaskAsync(failureResult, cancellationToken: cancellationToken);
            }
            catch (Exception resultEx)
            {
                _logger.LogGrpcProtocolHandlerActivityProcessorFailedToSendError(resultEx, request.Name);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeWorkItemCount);
        }
    }

    /// <summary>
    /// Creates a failure response for an activity exception.
    /// </summary>
    private static ActivityResponse CreateActivityFailureResult(ActivityRequest request, Exception ex) =>
        new()
        {
            
            InstanceId = request.OrchestrationInstance.InstanceId,
            FailureDetails = new()
            {
                ErrorType = ex.GetType().FullName ?? "Exception",
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace
            }
        };

    /// <summary>
    /// Creates a failure result for an orchestrator exception.
    /// </summary>
    private static OrchestratorResponse CreateWorkflowFailureResult(OrchestratorRequest request, Exception ex) =>
        new()
        {
            InstanceId = request.InstanceId,
            Actions =
            {
                new OrchestratorAction
                {
                    CompleteOrchestration = new CompleteOrchestrationAction
                    {
                        OrchestrationStatus = OrchestrationStatus.Failed,
                        FailureDetails = new()
                        {
                            ErrorType = ex.GetType().FullName ?? "Exception",
                            ErrorMessage = ex.Message,
                            StackTrace = ex.StackTrace
                        }
                    }
                }
            }
        };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposalCts.IsCancellationRequested)
            return;
        
        _logger.LogGrpcProtocolHandlerDisposing();
        
        await _disposalCts.CancelAsync();
        _streamingCall?.Dispose();
        _disposalCts.Dispose();
        
        _logger.LogGrpcProtocolHandlerDisposed();
    }
}
