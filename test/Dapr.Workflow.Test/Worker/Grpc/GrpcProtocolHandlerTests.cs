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
