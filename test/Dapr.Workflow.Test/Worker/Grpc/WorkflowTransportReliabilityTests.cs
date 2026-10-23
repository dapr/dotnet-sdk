// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using System.Threading.Channels;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Worker;
using Dapr.Workflow.Worker.Grpc;
using Dapr.Workflow.Worker.Internal;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

#pragma warning disable CS0612 // Exercise the completion RPC used for compatibility with pre-1.18 runtimes.

namespace Dapr.Workflow.Test.Worker.Grpc;

public sealed class WorkflowTransportReliabilityTests
{
    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> Client() =>
        new(new Mock<CallInvoker>().Object);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static AsyncUnaryCall<T> Unary<T>(T value) => new(
        Task.FromResult(value), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
    private static AsyncServerStreamingCall<WorkItem> Stream(IAsyncStreamReader<WorkItem> reader) =>
        new(reader, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });

    [Theory]
    [InlineData("")]
    [InlineData("workflow-token")]
    public async Task StreamingHistory_PreservesAuthenticationUserAgentAndCancellation(string apiToken)
    {
        var grpc = Client();
        CallOptions? captured = null;
        grpc.Setup(c => c.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(), It.IsAny<CallOptions>()))
            .Callback<GetInstanceHistoryRequest, CallOptions>((_, options) => captured = options)
            .Returns(Unary(new GetInstanceHistoryResponse()));
        grpc.Setup(c => c.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Returns(Unary(new CompleteTaskResponse()));
        await using var handler = new GrpcProtocolHandler(grpc.Object, NullLoggerFactory.Instance, apiToken);
        Func<WorkflowRequest, string, Task<WorkflowResponse>> execute = (_, _) => Task.FromResult(new WorkflowResponse());
        await Invoke(handler, "ProcessWorkflowAsync",
            new WorkflowRequest { InstanceId = "instance", RequiresHistoryStreaming = true },
            "completion", execute, (Action)(() => Assert.Fail("Unexpected stream retirement")), 0,
            TestContext.Current.CancellationToken);
        Assert.NotNull(captured);
        Assert.Equal(TestContext.Current.CancellationToken, captured.Value.CancellationToken);
        Assert.Contains(captured.Value.Headers!, entry =>
            entry.Key.Equals("user-agent", StringComparison.OrdinalIgnoreCase) &&
            entry.Value.Contains("dapr-sdk-dotnet", StringComparison.OrdinalIgnoreCase));
        var token = captured.Value.Headers!.FirstOrDefault(entry => entry.Key == "dapr-api-token");
        if (apiToken.Length == 0)
            Assert.Null(token);
        else
            Assert.Equal(apiToken, token?.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCompletion_RetiresOwningStream_WithoutSendingApplicationFailure(bool activity)
    {
        var grpc = Client();
        var responses = new List<object>();
        grpc.Setup(c => c.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback<WorkflowResponse, CallOptions>((response, _) => responses.Add(response))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "delivery failed")));
        grpc.Setup(c => c.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((response, _) => responses.Add(response))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "delivery failed")));
        await using var handler = new GrpcProtocolHandler(grpc.Object, NullLoggerFactory.Instance);
        var retired = false;
        if (activity)
        {
            Func<ActivityRequest, string, Task<ActivityResponse>> execute = (r, token) =>
                Task.FromResult(new ActivityResponse { InstanceId = r.WorkflowInstance.InstanceId, CompletionToken = token, Result = "42" });
            await Invoke(handler, "ProcessActivityAsync",
                new ActivityRequest { Name = "activity", WorkflowInstance = new() { InstanceId = "instance" } },
                "completion", execute, TestContext.Current.CancellationToken, (Action)(() => retired = true));
            Assert.Null(Assert.IsType<ActivityResponse>(Assert.Single(responses)).FailureDetails);
        }
        else
        {
            Func<WorkflowRequest, string, Task<WorkflowResponse>> execute = (r, token) =>
                Task.FromResult(new WorkflowResponse { InstanceId = r.InstanceId, CompletionToken = token });
            await Invoke(handler, "ProcessWorkflowAsync", new WorkflowRequest { InstanceId = "instance" },
                "completion", execute, (Action)(() => retired = true), 0, TestContext.Current.CancellationToken);
            Assert.Empty(Assert.IsType<WorkflowResponse>(Assert.Single(responses)).Actions);
        }
        Assert.True(retired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamedHistoryFailure_RetiresStreamBeforeExecutingWorkflow(bool asynchronous)
    {
        var grpc = Client();
        var error = new RpcException(new Status(StatusCode.Unavailable, "history unavailable"));
        var setup = grpc.Setup(c => c.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(), It.IsAny<CallOptions>()));
        if (asynchronous)
            setup.Returns(new AsyncUnaryCall<GetInstanceHistoryResponse>(Task.FromException<GetInstanceHistoryResponse>(error),
                Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { }));
        else
            setup.Throws(error);
        await using var handler = new GrpcProtocolHandler(grpc.Object, NullLoggerFactory.Instance);
        var executed = false;
        var retired = false;
        Func<WorkflowRequest, string, Task<WorkflowResponse>> execute = (_, _) =>
        {
            executed = true;
            return Task.FromResult(new WorkflowResponse());
        };
        await Invoke(handler, "ProcessWorkflowAsync",
            new WorkflowRequest { InstanceId = "instance", RequiresHistoryStreaming = true },
            "completion", execute, (Action)(() => retired = true), 0, TestContext.Current.CancellationToken);
        Assert.True(retired);
        Assert.False(executed);
        grpc.Verify(c => c.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(),
            It.Is<CallOptions>(o => o.CancellationToken == TestContext.Current.CancellationToken)), Times.Once);
        grpc.Verify(c => c.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LifecycleControl_IsHonoredBeforeLaterTaskCompletion(bool terminate) => Task.Run(async () =>
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);
        factory.RegisterWorkflow<object?, string>("workflow",
            async (context, _) => await context.CallActivityAsync<string>("activity"));
        var worker = new WorkflowWorker(Client().Object, factory, NullLoggerFactory.Instance,
            new JsonDaprSerializer(), provider);
        var request = new WorkflowRequest
        {
            InstanceId = "instance",
            PastEvents =
            {
                new HistoryEvent { ExecutionStarted = new() { Name = "workflow" } },
                new HistoryEvent { EventId = 0, TaskScheduled = new() { Name = "activity" } }
            },
            NewEvents =
            {
                terminate ? new HistoryEvent { ExecutionTerminated = new() } : new HistoryEvent { ExecutionSuspended = new() },
                new HistoryEvent { TaskCompleted = new() { TaskScheduledId = 0, Result = "\"done\"" } }
            }
        };
        var method = typeof(WorkflowWorker).GetMethod("HandleWorkflowResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var response = await (Task<WorkflowResponse>)method.Invoke(worker, [request, "completion"])!;
        if (terminate)
            Assert.Equal(OrchestrationStatus.Terminated, Assert.Single(response.Actions).CompleteWorkflow.WorkflowStatus);
        else
            Assert.Empty(response.Actions);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndedStream_CancelsQueuedWorkBeforeReconnect(bool graceful)
    {
        var grpc = Client();
        var entered = Signal();
        var release = Signal();
        var allFinished = Signal();
        var reconnected = Signal();
        var enteredCount = 0;
        var finishedCount = 0;
        var connections = 0;
        CancellationToken retiredToken = default;
        grpc.Setup(c => c.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns((GetWorkItemsRequest _, CallOptions options) =>
            {
                if (Interlocked.Increment(ref connections) == 1)
                {
                    retiredToken = options.CancellationToken;
                    return Stream(new BacklogReader(entered.Task, graceful));
                }
                reconnected.TrySetResult();
                return Stream(new ChannelStream(Channel.CreateUnbounded<WorkItem>().Reader));
            });
        grpc.Setup(c => c.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Returns(Unary(new CompleteTaskResponse()));
        await using var handler = new GrpcProtocolHandler(grpc.Object, NullLoggerFactory.Instance);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = handler.StartAsync((_, _) => Task.FromResult(new WorkflowResponse()), async (_, _) =>
        {
            if (Interlocked.Increment(ref enteredCount) == 100)
                entered.TrySetResult();
            await release.Task;
            if (Interlocked.Increment(ref finishedCount) == 100)
                allFinished.TrySetResult();
            return new ActivityResponse();
        }, stopping.Token);
        try
        {
            await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(12), stopping.Token);
            Assert.True(retiredToken.IsCancellationRequested);
            release.TrySetResult();
            await allFinished.Task.WaitAsync(TimeSpan.FromSeconds(3), stopping.Token);
            await WaitForIdle(handler, stopping.Token);
            Assert.Equal(100, Volatile.Read(ref enteredCount));
            grpc.Verify(c => c.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()), Times.Never);
        }
        finally
        {
            release.TrySetResult();
            await stopping.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CleanEof_DoesNotBlockShutdownOrDisposeOnUncooperativeActivity()
    {
        var grpc = Client();
        var entered = Signal();
        var release = Signal();
        var channel = Channel.CreateUnbounded<WorkItem>();
        channel.Writer.TryWrite(new WorkItem { ActivityRequest = new() { Name = "activity", WorkflowInstance = new() { InstanceId = "instance" } } });
        channel.Writer.Complete();
        grpc.Setup(c => c.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(Stream(new ChannelStream(channel.Reader)));
        await using var handler = new GrpcProtocolHandler(grpc.Object, NullLoggerFactory.Instance);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = handler.StartAsync((_, _) => Task.FromResult(new WorkflowResponse()), async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return new ActivityResponse();
        }, stopping.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), stopping.Token);
            await stopping.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await handler.DisposeAsync();
        }
        finally
        {
            release.TrySetResult();
            await stopping.CancelAsync();
            await run.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await WaitForIdle(handler, TestContext.Current.CancellationToken);
        }
        grpc.Verify(c => c.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()), Times.Never);
    }

    private static Task Invoke(GrpcProtocolHandler handler, string name, params object[] arguments) =>
        (Task)typeof(GrpcProtocolHandler).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(handler, arguments)!;

    private static async Task WaitForIdle(GrpcProtocolHandler handler, CancellationToken cancellationToken)
    {
        var active = typeof(GrpcProtocolHandler).GetField("_activeWorkItemCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        while ((int)active.GetValue(handler)! != 0)
            await Task.Delay(10, timeout.Token);
    }

    private sealed class ChannelStream(ChannelReader<WorkItem> reader) : IAsyncStreamReader<WorkItem>
    {
        public WorkItem Current { get; private set; } = new();
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (!await reader.WaitToReadAsync(cancellationToken))
                return false;
            Current = await reader.ReadAsync(cancellationToken);
            return true;
        }
    }

    private sealed class BacklogReader(Task entered, bool graceful) : IAsyncStreamReader<WorkItem>
    {
        private int _sent;
        public WorkItem Current { get; private set; } = new();
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_sent < 101)
            {
                Current = new WorkItem { ActivityRequest = new() { Name = "activity", TaskId = _sent++, WorkflowInstance = new() { InstanceId = "instance" } } };
                return true;
            }
            await entered.WaitAsync(cancellationToken);
            if (!graceful)
                throw new RpcException(new Status(StatusCode.Unavailable, "disconnected"));
            return false;
        }
    }
}
