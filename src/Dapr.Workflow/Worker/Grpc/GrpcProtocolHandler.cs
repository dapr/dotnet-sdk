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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common;
using Dapr.DurableTask.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker.Grpc;

/// <summary>
/// Handles the bidirectional gRPC streaming protocol with the Dapr sidecar.
/// </summary>
internal sealed class GrpcProtocolHandler(
    TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient,
    ILoggerFactory loggerFactory,
    string? daprApiToken = null,
    bool disableStatefulHistory = false,
    TimeSpan? historyCacheTtl = null,
    int historyCacheMaxInstances = 0,
    long historyCacheMaxBytes = 0) : IAsyncDisposable {
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KeepaliveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HistorySweepInterval = TimeSpan.FromMinutes(1);

    private readonly CancellationTokenSource _disposalCts = new();
    private readonly ILogger<GrpcProtocolHandler> _logger = loggerFactory.CreateLogger<GrpcProtocolHandler>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly TaskHubSidecarService.TaskHubSidecarServiceClient _grpcClient =
        grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
    private readonly SemaphoreSlim _orchestrationSemaphore = new(100);
    private readonly SemaphoreSlim _activitySemaphore = new(100);

    private readonly bool _disableStatefulHistory = disableStatefulHistory;
    private readonly WorkflowHistoryCache _historyCache =
        new(historyCacheTtl, historyCacheMaxInstances, historyCacheMaxBytes);

    private AsyncServerStreamingCall<WorkItem>? _streamingCall;
    private int _activeWorkItemCount;
    private int _disposed;

    /// <summary>
    /// Starts the streaming connection with the Dapr sidecar.
    /// </summary>
    /// <param name="workflowHandler">Handler for workflow work items.</param>
    /// <param name="activityHandler">Handler for activity work items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(
        Func<WorkflowRequest, string, Task<WorkflowResponse>> workflowHandler,
        Func<ActivityRequest, string, Task<ActivityResponse>> activityHandler,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
        var token = linkedCts.Token;

        // Reclaim idle history-cache entries for the lifetime of this listener.
        var janitorTask = _disableStatefulHistory ? Task.CompletedTask : RunHistoryJanitorAsync(token);

        // Establish the bidirectional stream. Advertise stateful-history support so the
        // sidecar can send deltas instead of the full history each turn.
        var request = new GetWorkItemsRequest();
        if (!_disableStatefulHistory)
        {
            request.Capabilities.Add(WorkerCapability.StatefulHistory);
        }

        try
        {
        while (!token.IsCancellationRequested)
        {
            CancellationTokenSource? keepaliveCts = null;
            Task? keepaliveTask = null;

            try
            {
                _logger.LogGrpcProtocolHandlerStartStream();

                // Establish the server streaming call
                var grpcCallOptions = CreateCallOptions(token);
                _streamingCall = _grpcClient.GetWorkItems(request, grpcCallOptions);

                _logger.LogGrpcProtocolHandlerStreamEstablished();

                // Start the background keepalive loop to keep the connection alive
                keepaliveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                keepaliveTask = KeepaliveLoopAsync(keepaliveCts.Token);

                // Process work items from the stream
                await ReceiveLoopAsync(_streamingCall.ResponseStream, workflowHandler, activityHandler, token);

                // Stream ended gracefully => treat as an interrupted and reconnect unless shutting down
                if (!token.IsCancellationRequested)
                {
                    await DelayOrStopAsync(ReconnectDelay, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.LogGrpcProtocolHandlerStreamCanceled();
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && token.IsCancellationRequested)
            {
                _logger.LogGrpcProtocolHandlerStreamCanceled();
                break;
            }
            catch (RpcException ex) when (!token.IsCancellationRequested)
            {
                // Any RpcException while not shutting down -> retry indefinitely (transient or not)
                _logger.LogGrpcProtocolHandlerGenericError(ex);
                await DelayOrStopAsync(ReconnectDelay, token);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                // Any other interruption -> retry indefinitely
                _logger.LogGrpcProtocolHandlerGenericError(ex);
                await DelayOrStopAsync(ReconnectDelay, token);
            }
            finally
            {
                // Stop the keepalive loop when the receive loop ends (reconnect or shutdown).
                // This runs after catch filters evaluate, avoiding a race where teardown delay
                // allows external cancellation to change filter outcomes.
                if (keepaliveCts != null)
                {
                    await keepaliveCts.CancelAsync();
                    try { await keepaliveTask!; }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { _logger.LogGrpcProtocolHandlerKeepaliveFailed(ex); }
                    keepaliveCts.Dispose();
                }

                _streamingCall?.Dispose();
                _streamingCall = null;

                // The next stream starts cold: the sidecar drops this stream's warm set,
                // so the cached histories from this connection are no longer in sync.
                _historyCache.Reset();
            }
        }
        }
        finally
        {
            await janitorTask;
        }
    }

    private async Task RunHistoryJanitorAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(HistorySweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                _historyCache.SweepExpired();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private static async Task DelayOrStopAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Swallow cancellation so StartAsync exits cleanly when the host/test cancels.
        }
    }

    /// <summary>
    /// Receives requests from the Dapr sidecar and processes them.
    /// </summary>
    private async Task ReceiveLoopAsync(
        IAsyncStreamReader<WorkItem> workItemsStream,
        Func<WorkflowRequest, string, Task<WorkflowResponse>> workflowHandler,
        Func<ActivityRequest, string, Task<ActivityResponse>> activityHandler,
        CancellationToken cancellationToken)
    {
        // Track active work items for proper exception handling
        var activeWorkItems = new List<Task>();

        try
        {
            await foreach (var workItem in workItemsStream.ReadAllAsync(cancellationToken))
            {
                var completionToken = workItem.CompletionToken;
                
                // Dispatch based on work item type
                var workItemTask = workItem.RequestCase switch
                {
                    WorkItem.RequestOneofCase.WorkflowRequest => Task.Run(
                        () => ProcessWorkflowAsync(workItem.WorkflowRequest, completionToken, workflowHandler, cancellationToken),
                        cancellationToken),
                    WorkItem.RequestOneofCase.ActivityRequest => Task.Run(
                        () => ProcessActivityAsync(workItem.ActivityRequest, completionToken, activityHandler, cancellationToken),
                        cancellationToken),
                    _ => Task.Run(
                        () => _logger.LogGrpcProtocolHandlerUnknownWorkItemType(workItem.RequestCase),
                        cancellationToken)
                };

                activeWorkItems.Add(workItemTask);

                // Clean up completed tasks periodically
                if (activeWorkItems.Count > 200)
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown path (host stopping / handler disposing / token canceled)
            _logger.LogGrpcProtocolHandlerReceiveLoopCanceled();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            // gRPC surfaces token/dispose cancellation as StatusCode.Cancelled
            _logger.LogGrpcProtocolHandlerReceiveLoopCanceled();
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
    private async Task ProcessWorkflowAsync(WorkflowRequest request, string completionToken,
        Func<WorkflowRequest, string, Task<WorkflowResponse>> handler, CancellationToken cancellationToken)
    {
        await _orchestrationSemaphore.WaitAsync(cancellationToken);
        var activeCount = Interlocked.Increment(ref _activeWorkItemCount);

        try
        {
            _logger.LogGrpcProtocolHandlerWorkflowProcessorStart(request.InstanceId, activeCount);

            // Resolve the committed history before replay. A delta work item (cachedHistory set)
            // carries only the events new since this worker was last warm for the instance, so we
            // rebuild the full history from our per-stream cache, or fetch it on a miss. Overwriting
            // request.PastEvents here keeps the workflow handler oblivious to the delta protocol.
            if (!_disableStatefulHistory && request.CachedHistory is not null)
            {
                try
                {
                    await ResolveCachedHistoryAsync(request, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogGrpcProtocolHandlerWorkflowProcessorCanceled(request.InstanceId);
                    return;
                }
                catch (Exception ex)
                {
                    // The cache-miss fallback fetch failed and there is no per-item NACK. Abandon
                    // the work item so the backend redelivers it (as a full-history send on a future
                    // stream) rather than marking an otherwise-healthy turn as failed.
                    _logger.LogGrpcProtocolHandlerWorkflowProcessorFailedToSendError(ex, request.InstanceId);
                    return;
                }
            }

            // Execute the orchestrator and determine the response (normal actions or application failure).
            // This try/catch must NOT include the CompleteOrchestratorTaskAsync call below — a transport
            // failure during delivery must not be converted into an orchestrator-level failure, as that
            // would incorrectly mark a healthy workflow turn as failed.
            WorkflowResponse result;
            try
            {
                result = await handler(request, completionToken);
                UpdateHistoryCache(request, result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogGrpcProtocolHandlerWorkflowProcessorCanceled(request.InstanceId);
                return;
            }
            catch (Exception ex)
            {
                result = CreateWorkflowFailureResult(request, completionToken, ex);
            }

            // Send the result back to Dapr. If delivery fails (e.g. transient gRPC error or
            // "no such instance exists"), log and abandon — do NOT send a secondary failure
            // response, as that would corrupt the workflow history.
            try
            {
                var grpcCallOptions = CreateCallOptions(cancellationToken);
#pragma warning disable CS0612 // Deprecated: kept for compatibility with Dapr runtimes < 1.18 where CompleteWorkflowTask is not available.
                await _grpcClient.CompleteOrchestratorTaskAsync(result, grpcCallOptions);
#pragma warning restore CS0612
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogGrpcProtocolHandlerWorkflowProcessorCanceled(request.InstanceId);
            }
            catch (Exception resultEx)
            {
                _logger.LogGrpcProtocolHandlerWorkflowProcessorFailedToSendError(resultEx, request.InstanceId);
            }
        }
        finally
        {
            _orchestrationSemaphore.Release();
            Interlocked.Decrement(ref _activeWorkItemCount);
        }
    }

    /// <summary>
    /// Rebuilds the full committed history for a delta work item into <paramref name="request"/>.PastEvents:
    /// the cached prefix (validated against the sidecar's expected event count) plus the delta, or the
    /// full history fetched via GetInstanceHistory on a cache miss.
    /// </summary>
    private async Task ResolveCachedHistoryAsync(WorkflowRequest request, CancellationToken token)
    {
        var cached = _historyCache.Get(request.InstanceId);
        if (cached is not null && cached.Count == request.CachedHistory.EventCount)
        {
            var full = new List<HistoryEvent>(cached.Count + request.PastEvents.Count);
            full.AddRange(cached);
            full.AddRange(request.PastEvents);
            request.PastEvents.Clear();
            request.PastEvents.AddRange(full);
            return;
        }

        var historyRequest = new GetInstanceHistoryRequest { InstanceId = request.InstanceId };
        var response = await _grpcClient.GetInstanceHistoryAsync(historyRequest, CreateCallOptions(token));
        request.PastEvents.Clear();
        request.PastEvents.AddRange(response.Events);
    }

    /// <summary>
    /// Refreshes the per-stream history cache after a turn so the next turn can be served as a delta.
    /// Caches only the committed history just replayed (never the not-yet-committed NewEvents), and drops
    /// the entry once the instance ends (a CompleteWorkflow action, covering completed/failed/terminated/
    /// continued-as-new). Skipped when stateful history is disabled or the request used history streaming
    /// (which leaves PastEvents partial).
    /// </summary>
    private void UpdateHistoryCache(WorkflowRequest request, WorkflowResponse result)
    {
        if (_disableStatefulHistory || request.RequiresHistoryStreaming)
        {
            return;
        }

        var ended = result.Actions.Any(a => a.CompleteWorkflow is not null);
        if (ended)
        {
            _historyCache.Remove(request.InstanceId);
        }
        else
        {
            _historyCache.Put(request.InstanceId, request.PastEvents);
        }
    }

    /// <summary>
    /// Processes an activity request work item.
    /// </summary>
    private async Task ProcessActivityAsync(ActivityRequest request, string completionToken,
        Func<ActivityRequest, string, Task<ActivityResponse>> handler, CancellationToken cancellationToken)
    {
        await _activitySemaphore.WaitAsync(cancellationToken);
        var activeCount = Interlocked.Increment(ref _activeWorkItemCount);

        try
        {
            _logger.LogGrpcProtocolHandlerActivityProcessorStart(request.WorkflowInstance.InstanceId, request.Name,
                request.TaskId, activeCount);

            // Restore the trace context provided by the sidecar so Activity.Current is non-null
            using var traceScope = WorkflowTrace.StartActivityTrace(request);

            // Execute the activity and determine the result (success or application failure).
            // This try/catch must NOT include the CompleteActivityTaskAsync call below — a transport
            // failure during delivery must not be converted into an application-level activity failure,
            // as that would incorrectly mark a successfully-executed activity as failed.
            ActivityResponse result;
            try
            {
                result = await handler(request, completionToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogGrpcProtocolHandlerActivityProcessorCanceled(request.Name);
                return;
            }
            catch (Exception ex)
            {
                WorkflowTrace.SetCurrentError(ex);
                _logger.LogGrpcProtocolHandlerActivityProcessorError(ex, request.Name,
                    request.WorkflowInstance?.InstanceId);
                result = CreateActivityFailureResult(request, completionToken, ex);
            }

            // Send the result back to Dapr. If delivery fails (e.g. transient gRPC error or
            // "no such instance exists"), log and abandon — do NOT send a secondary failure
            // response, as that would corrupt the workflow history.
            try
            {
                var grpcCallOptions = CreateCallOptions(cancellationToken);
                await _grpcClient.CompleteActivityTaskAsync(result, grpcCallOptions);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogGrpcProtocolHandlerActivityProcessorCanceled(request.Name);
            }
            catch (Exception resultEx)
            {
                _logger.LogGrpcProtocolHandlerActivityProcessorFailedToSendError(resultEx, request.Name);
            }
        }
        finally
        {
            _activitySemaphore.Release();
            Interlocked.Decrement(ref _activeWorkItemCount);
        }
    }

    /// <summary>
    /// Creates a failure response for an activity exception.
    /// </summary>
    private static ActivityResponse CreateActivityFailureResult(ActivityRequest request, string completionToken, Exception ex) =>
        new()
        {
            InstanceId = request.WorkflowInstance.InstanceId,
            TaskId = request.TaskId,
            CompletionToken = completionToken,
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
    private static WorkflowResponse CreateWorkflowFailureResult(WorkflowRequest request, string completionToken, Exception ex) =>
        new()
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
                            ErrorType = ex.GetType().FullName ?? "Exception",
                            ErrorMessage = ex.Message,
                            StackTrace = ex.StackTrace
                        }
                    }
                }
            }
        };

    /// <summary>
    /// Periodically calls Hello on the sidecar to prevent idle HTTP/2 connections from being
    /// closed by intermediary load balancers (e.g. AWS ALB).
    /// </summary>
    private async Task KeepaliveLoopAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(KeepaliveInterval);
        while (await timer.WaitForNextTickAsync(cancellation))
        {
            try
            {
                await _grpcClient.HelloAsync(new Empty(), CreateCallOptions(cancellation));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogGrpcProtocolHandlerKeepaliveFailed(ex);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _logger.LogGrpcProtocolHandlerDisposing();

        try { await _disposalCts.CancelAsync(); }
        catch (ObjectDisposedException) { }

        _streamingCall?.Dispose();
        _disposalCts.Dispose();
        _orchestrationSemaphore.Dispose();
        _activitySemaphore.Dispose();

        _logger.LogGrpcProtocolHandlerDisposed();
    }

    private CallOptions CreateCallOptions(CancellationToken cancellationToken) =>
        DaprClientUtilities.ConfigureGrpcCallOptions(typeof(GrpcProtocolHandler).Assembly, daprApiToken, cancellationToken);
}
