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
//  ------------------------------------------------------------------------

using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Worker.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test.Worker.Grpc;

public class GrpcProtocolHandlerTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerFactoryIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() => new GrpcProtocolHandler(grpcClient, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenMaxConcurrentWorkItemsIsNotPositive(int value)
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentOutOfRangeException>(() => new GrpcProtocolHandler(grpcClient, NullLoggerFactory.Instance, value, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenMaxConcurrentActivitiesIsNotPositive(int value)
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentOutOfRangeException>(() => new GrpcProtocolHandler(grpcClient, NullLoggerFactory.Instance, 1, value));
    }

    [Fact]
    public async Task StartAsync_ShouldCompleteOrchestratorTask_ForOrchestratorWorkItem()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                OrchestratorRequest = new OrchestratorRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        OrchestratorResponse? completed = null;
        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<OrchestratorResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<OrchestratorResponse, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => completed = r)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: req => Task.FromResult(new OrchestratorResponse { InstanceId = req.InstanceId }),
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal("i-1", completed!.InstanceId);
    }

    [Fact]
    public async Task StartAsync_ShouldCompleteActivityTask_ForActivityWorkItem()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 42,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "i-2" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        ActivityResponse? completed = null;
        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<ActivityResponse, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => completed = r)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: req => Task.FromResult(new ActivityResponse { InstanceId = req.OrchestrationInstance.InstanceId, TaskId = req.TaskId, Result = "ok" }),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal("i-2", completed!.InstanceId);
        Assert.Equal(42, completed.TaskId);
        Assert.Equal("ok", completed.Result);
    }

    [Fact]
    public async Task StartAsync_ShouldSendFailureResult_WhenOrchestratorHandlerThrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                OrchestratorRequest = new OrchestratorRequest { InstanceId = "i-err" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        OrchestratorResponse? completed = null;
        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<OrchestratorResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<OrchestratorResponse, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => completed = r)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => throw new InvalidOperationException("boom"),
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal("i-err", completed!.InstanceId);
        Assert.Single(completed.Actions);
        Assert.NotNull(completed.Actions[0].CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.Failed, completed.Actions[0].CompleteOrchestration.OrchestrationStatus);
        Assert.NotNull(completed.Actions[0].CompleteOrchestration.FailureDetails);
        Assert.Contains("boom", completed.Actions[0].CompleteOrchestration.FailureDetails.ErrorMessage);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent()
    {
        var grpcClientMock = CreateGrpcClientMock();
        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.DisposeAsync();
        await handler.DisposeAsync();
    }
    
    [Fact]
    public async Task StartAsync_ShouldSendGetWorkItemsRequest_WithConfiguredConcurrencyLimits()
    {
        var grpcClientMock = CreateGrpcClientMock();

        GetWorkItemsRequest? captured = null;

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns((GetWorkItemsRequest r, Metadata? _, DateTime? __, CancellationToken ___) =>
            {
                captured = r;
                return CreateServerStreamingCall(Array.Empty<WorkItem>());
            });

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance, maxConcurrentWorkItems: 7, maxConcurrentActivities: 9);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(7, captured!.MaxConcurrentOrchestrationWorkItems);
        Assert.Equal(9, captured.MaxConcurrentActivityWorkItems);
    }

    [Fact]
    public async Task StartAsync_ShouldReturnWithoutThrowing_WhenGrpcStreamIsCancelled()
    {
        var grpcClientMock = CreateGrpcClientMock();

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Cancelled, "cancelled")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldSendActivityFailureResult_WhenActivityHandlerThrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 123,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        ActivityResponse? sent = null;
        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<ActivityResponse, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ => throw new InvalidOperationException("boom"),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal("i-1", sent!.InstanceId);
        Assert.Equal(0, sent.TaskId);
        Assert.NotNull(sent.FailureDetails);
        Assert.Contains(nameof(InvalidOperationException), sent.FailureDetails.ErrorType);
        Assert.Contains("boom", sent.FailureDetails.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_ShouldNotThrow_WhenSendingActivityFailureResultAlsoThrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 123,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "nope")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ => throw new Exception("boom"),
            cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldUseCreateActivityFailureResult_WithNullStackTrace_WhenExceptionStackTraceIsNull()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 123,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        ActivityResponse? sent = null;
        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<ActivityResponse, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => sent = r)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ => throw new NullStackTraceException("boom"),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(sent);
        Assert.NotNull(sent!.FailureDetails);
        Assert.Null(sent.FailureDetails.StackTrace);
        Assert.Contains("boom", sent.FailureDetails.ErrorMessage);
    }
    
    [Fact]
    public async Task StartAsync_ShouldNotCallCompleteActivityTask_WhenActivityHandlerThrowsOperationCanceledException_AndTokenIsCanceled()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 1,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCallIgnoringCancellation(workItems));

        var completeCalled = false;
        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback(() => completeCalled = true)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        using var cts = new CancellationTokenSource();

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ =>
            {
                cts.Cancel(); // make StartAsync's linked token "IsCancellationRequested == true"
                throw new OperationCanceledException(cts.Token);
            },
            cancellationToken: cts.Token);

        Assert.False(completeCalled);
    }

    [Fact]
    public async Task StartAsync_ShouldNotCallCompleteOrchestratorTask_WhenWorkflowHandlerThrowsOperationCanceledException_AndTokenIsCanceled()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                OrchestratorRequest = new OrchestratorRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCallIgnoringCancellation(workItems));

        var completeCalled = false;
        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<OrchestratorResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback(() => completeCalled = true)
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        using var cts = new CancellationTokenSource();

        await handler.StartAsync(
            workflowHandler: _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: cts.Token);

        Assert.False(completeCalled);
    }

    [Fact]
    public async Task StartAsync_ShouldCleanupCompletedActiveWorkItems_WhenActiveWorkItemsListGrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        // With maxConcurrentWorkItems=1, cleanup threshold is > 2.
        // We send 3 work items that complete quickly so RemoveAll(t => t.IsCompleted) is executed.
        var workItems = new[]
        {
            new WorkItem { ActivityRequest = new ActivityRequest { Name = "a1", TaskId = 1, OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" } } },
            new WorkItem { ActivityRequest = new ActivityRequest { Name = "a2", TaskId = 2, OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" } } },
            new WorkItem { ActivityRequest = new ActivityRequest { Name = "a3", TaskId = 3, OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" } } },
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedCount = 0;
        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref completedCount))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance, maxConcurrentWorkItems: 1, maxConcurrentActivities: 1);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: req => Task.FromResult(new ActivityResponse { InstanceId = req.OrchestrationInstance.InstanceId, TaskId = req.TaskId, Result = "ok" }),
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, completedCount);
    }
    
    [Fact]
    public async Task StartAsync_ShouldNotThrow_WhenWorkflowHandlerThrows_AndSendingFailureResultAlsoThrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                OrchestratorRequest = new OrchestratorRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<OrchestratorResponse>(), null, null, It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "nope")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => throw new InvalidOperationException("boom"),
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ShouldHandleUnknownWorkItemType_AndWaitForActiveTasks()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem() // RequestCase = None
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(workItems));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await handler.StartAsync(
            workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
            activityHandler: _ => Task.FromResult(new ActivityResponse()),
            cancellationToken: CancellationToken.None);
    }
    
    [Fact]
    public async Task StartAsync_ShouldRethrow_WhenReceiveLoopThrowsBeforeAnyItemsAreRead()
    {
        var grpcClientMock = CreateGrpcClientMock();

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCallFromReader(new ThrowingAsyncStreamReader(new InvalidOperationException("boom"))));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.StartAsync(
                workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
                activityHandler: _ => Task.FromResult(new ActivityResponse()),
                cancellationToken: CancellationToken.None));

        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task StartAsync_ShouldRethrow_WhenReceiveLoopThrowsAfterFirstItemIsRead()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var reader = new ThrowingAfterOneAsyncStreamReader(
            first: new WorkItem(), // RequestCase = None => Task.Run branch
            thenThrow: new InvalidOperationException("boom-after-one"));

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCallFromReader(reader));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.StartAsync(
                workflowHandler: _ => Task.FromResult(new OrchestratorResponse()),
                activityHandler: _ => Task.FromResult(new ActivityResponse()),
                cancellationToken: CancellationToken.None));

        Assert.Contains("boom-after-one", ex.Message);
    }
    
    private static AsyncServerStreamingCall<WorkItem> CreateServerStreamingCallFromReader(IAsyncStreamReader<WorkItem> reader)
    {
        return new AsyncServerStreamingCall<WorkItem>(
            reader,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

    private sealed class ThrowingAsyncStreamReader(Exception ex) : IAsyncStreamReader<WorkItem>
    {
        public WorkItem Current => new();

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw ex;
        }
    }

    private sealed class ThrowingAfterOneAsyncStreamReader(WorkItem first, Exception thenThrow) : IAsyncStreamReader<WorkItem>
    {
        private int _state; // 0 = before first, 1 = after first (throw), 2 = done (never reached)

        public WorkItem Current { get; private set; } = new();

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_state == 0)
            {
                _state = 1;
                Current = first;
                return Task.FromResult(true);
            }

            throw thenThrow;
        }
    }
    
    private static AsyncServerStreamingCall<WorkItem> CreateServerStreamingCallIgnoringCancellation(IEnumerable<WorkItem> items)
    {
        var stream = new TestAsyncStreamReaderIgnoringCancellation(items);

        return new AsyncServerStreamingCall<WorkItem>(
            stream,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

    private sealed class TestAsyncStreamReaderIgnoringCancellation(IEnumerable<WorkItem> items) : IAsyncStreamReader<WorkItem>
    {
        private readonly IEnumerator<WorkItem> _enumerator = items.GetEnumerator();

        public WorkItem Current { get; private set; } = new();

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            // Intentionally ignore cancellationToken to allow testing of the
            // OperationCanceledException paths inside ProcessWorkflowAsync/ProcessActivityAsync.
            var moved = _enumerator.MoveNext();
            if (moved)
            {
                Current = _enumerator.Current;
            }

            return Task.FromResult(moved);
        }
    }
    
    private sealed class NullStackTraceException(string message) : Exception(message)
    {
        public override string? StackTrace => null;
    }

    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> CreateGrpcClientMock()
    {
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        return new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(MockBehavior.Loose, callInvoker.Object);
    }

    private static AsyncServerStreamingCall<WorkItem> CreateServerStreamingCall(IEnumerable<WorkItem> items)
    {
        var stream = new TestAsyncStreamReader(items);

        return new AsyncServerStreamingCall<WorkItem>(
            stream,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

    private static AsyncUnaryCall<CompleteTaskResponse> CreateAsyncUnaryCall(CompleteTaskResponse response)
    {
        return new AsyncUnaryCall<CompleteTaskResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

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
}
