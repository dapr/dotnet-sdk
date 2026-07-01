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
//  ------------------------------------------------------------------------

using System.Collections.Concurrent;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Worker.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

#pragma warning disable CS0612 // Tests reference deprecated CompleteOrchestratorTaskAsync intentionally for compatibility with Dapr runtimes < 1.18.

namespace Dapr.Workflow.Test.Worker.Grpc;

/// <summary>
/// Flow tests for the worker's stateful-history protocol: capability advertisement and the
/// resolve-before / cache-after of committed history around each workflow turn, including the
/// GetInstanceHistory fallback on a cache miss. Exercised end to end through the real
/// <see cref="GrpcProtocolHandler"/> work-item stream. Mirrors the Go reference and the Python SDK.
/// </summary>
public sealed class GrpcProtocolHandlerStatefulHistoryTests
{
    private static List<HistoryEvent> Events(int count)
    {
        var events = new List<HistoryEvent>(count);
        for (var i = 0; i < count; i++)
        {
            events.Add(new HistoryEvent { EventId = i + 1 });
        }

        return events;
    }

    [Fact]
    public async Task AdvertisesStatefulHistoryCapability_ByDefault()
    {
        var grpcClientMock = CreateGrpcClientMock();

        GetWorkItemsRequest? capturedRequest = null;
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback<GetWorkItemsRequest, CallOptions>((r, _) => capturedRequest ??= r)
            .Returns(CreateServerStreamingCall([]));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(handler, AcceptingWorkflowHandler, NoActivityHandler,
            untilCondition: () => capturedRequest is not null, timeout: TimeSpan.FromSeconds(2));

        Assert.NotNull(capturedRequest);
        Assert.Contains(WorkerCapability.StatefulHistory, capturedRequest!.Capabilities);
    }

    [Fact]
    public async Task DoesNotAdvertiseCapability_WhenDisabled()
    {
        var grpcClientMock = CreateGrpcClientMock();

        GetWorkItemsRequest? capturedRequest = null;
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback<GetWorkItemsRequest, CallOptions>((r, _) => capturedRequest ??= r)
            .Returns(CreateServerStreamingCall([]));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance,
            disableStatefulHistory: true);

        await RunHandlerUntilAsync(handler, AcceptingWorkflowHandler, NoActivityHandler,
            untilCondition: () => capturedRequest is not null, timeout: TimeSpan.FromSeconds(2));

        Assert.NotNull(capturedRequest);
        Assert.Empty(capturedRequest!.Capabilities);
    }

    [Fact]
    public async Task FullSend_PassesPastEventsUnchanged_AndDoesNotFetchHistory()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem { WorkflowRequest = new WorkflowRequest { InstanceId = "i-1", PastEvents = { Events(2) } } }
        };
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completed = 0;
        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback(() => Interlocked.Increment(ref completed))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var seenPastEvents = new ConcurrentQueue<int>();
        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(handler,
            workflowHandler: (req, _) =>
            {
                seenPastEvents.Enqueue(req.PastEvents.Count);
                return Task.FromResult(new WorkflowResponse { InstanceId = req.InstanceId });
            },
            NoActivityHandler,
            untilCondition: () => Volatile.Read(ref completed) >= 1, timeout: TimeSpan.FromSeconds(2));

        Assert.Equal([2], seenPastEvents);
        grpcClientMock.Verify(
            x => x.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(), It.IsAny<CallOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task DeltaCacheMiss_FetchesFullHistoryViaGetInstanceHistory()
    {
        var grpcClientMock = CreateGrpcClientMock();

        // A delta work item whose expected prefix the (cold) cache does not hold.
        var delta = new WorkflowRequest { InstanceId = "i-1", PastEvents = { Events(1) } };
        delta.CachedHistory = new CachedHistory { EventCount = 5 };
        var workItems = new[] { new WorkItem { WorkflowRequest = delta } };
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var fetchCount = 0;
        grpcClientMock
            .Setup(x => x.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => Interlocked.Increment(ref fetchCount))
            .Returns(CreateAsyncUnaryCall(new GetInstanceHistoryResponse { Events = { Events(7) } }));

        var completed = 0;
        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback(() => Interlocked.Increment(ref completed))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var seenPastEvents = new ConcurrentQueue<int>();
        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(handler,
            workflowHandler: (req, _) =>
            {
                seenPastEvents.Enqueue(req.PastEvents.Count);
                return Task.FromResult(new WorkflowResponse { InstanceId = req.InstanceId });
            },
            NoActivityHandler,
            untilCondition: () => Volatile.Read(ref completed) >= 1, timeout: TimeSpan.FromSeconds(2));

        Assert.Equal([7], seenPastEvents); // recovered the full history via GetInstanceHistory
        Assert.Equal(1, Volatile.Read(ref fetchCount));
    }

    [Fact]
    public async Task DeltaCacheHit_ReconstructsCachedPrefixPlusDelta()
    {
        var grpcClientMock = CreateGrpcClientMock();

        // Turn 1 is a full send that warms the cache; turn 2 is a delta the cache can satisfy. Turn 2 is
        // gated on turn 1's completion so the post-turn cache update is guaranteed to have run first.
        var turn1 = new WorkItem { WorkflowRequest = new WorkflowRequest { InstanceId = "i-1", PastEvents = { Events(2) } } };
        var delta = new WorkflowRequest { InstanceId = "i-1", PastEvents = { Events(1) } };
        delta.CachedHistory = new CachedHistory { EventCount = 2 };
        var turn2 = new WorkItem { WorkflowRequest = delta };

        var turn1Completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCallFromReader(
                new GatedStreamReader([(turn1, null), (turn2, turn1Completed.Task)])));

        var completed = 0;
        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback(() =>
            {
                Interlocked.Increment(ref completed);
                turn1Completed.TrySetResult();
            })
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var seenPastEvents = new ConcurrentQueue<int>();
        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(handler,
            workflowHandler: (req, _) =>
            {
                seenPastEvents.Enqueue(req.PastEvents.Count);
                return Task.FromResult(new WorkflowResponse { InstanceId = req.InstanceId });
            },
            NoActivityHandler,
            untilCondition: () => Volatile.Read(ref completed) >= 2, timeout: TimeSpan.FromSeconds(5));

        Assert.Equal([2, 3], seenPastEvents); // turn 1 full (2); turn 2 cached prefix (2) + delta (1)
        grpcClientMock.Verify(
            x => x.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(), It.IsAny<CallOptions>()),
            Times.Never);
    }

    // --- harness -----------------------------------------------------------------------

    private static Task<WorkflowResponse> AcceptingWorkflowHandler(WorkflowRequest req, string _) =>
        Task.FromResult(new WorkflowResponse { InstanceId = req.InstanceId });

    private static Task<ActivityResponse> NoActivityHandler(ActivityRequest _, string __) =>
        Task.FromResult(new ActivityResponse());

    private static async Task RunHandlerUntilAsync(
        GrpcProtocolHandler handler,
        Func<WorkflowRequest, string, Task<WorkflowResponse>> workflowHandler,
        Func<ActivityRequest, string, Task<ActivityResponse>> activityHandler,
        Func<bool> untilCondition,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var runTask = handler.StartAsync(workflowHandler, activityHandler, cts.Token);
        try
        {
            while (!untilCondition())
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromMilliseconds(10), cts.Token);
            }
        }
        finally
        {
            cts.Cancel();
            await runTask;
        }
    }

    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> CreateGrpcClientMock()
    {
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        return new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(MockBehavior.Loose, callInvoker.Object);
    }

    private static AsyncServerStreamingCall<WorkItem> CreateServerStreamingCall(IEnumerable<WorkItem> items) =>
        CreateServerStreamingCallFromReader(new TestAsyncStreamReader(items));

    private static AsyncServerStreamingCall<WorkItem> CreateServerStreamingCallFromReader(IAsyncStreamReader<WorkItem> reader) =>
        new(reader, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response) =>
        new(Task.FromResult(response), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });

    private sealed class TestAsyncStreamReader(IEnumerable<WorkItem> items) : IAsyncStreamReader<WorkItem>
    {
        private readonly IEnumerator<WorkItem> _enumerator = items.GetEnumerator();

        public WorkItem Current { get; private set; } = new();

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var moved = _enumerator.MoveNext();
            if (moved)
            {
                Current = _enumerator.Current;
            }

            return Task.FromResult(moved);
        }
    }

    /// <summary>Yields each work item only once its (optional) gate task has completed.</summary>
    private sealed class GatedStreamReader(IReadOnlyList<(WorkItem item, Task? gate)> items) : IAsyncStreamReader<WorkItem>
    {
        private int _index = -1;

        public WorkItem Current { get; private set; } = new();

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _index++;
            if (_index >= items.Count)
            {
                return false;
            }

            var (item, gate) = items[_index];
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            Current = item;
            return true;
        }
    }
}
