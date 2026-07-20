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

using System.Diagnostics;
using System.Reflection;
using System.Collections.Concurrent;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Worker.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

#pragma warning disable CS0612 // Tests reference deprecated CompleteOrchestratorTaskAsync intentionally for compatibility with Dapr runtimes < 1.18.

namespace Dapr.Workflow.Test.Worker.Grpc;

public sealed class GrpcProtocolHandlerTests
{
    private static TaskCompletionSource<T> CreateTcs<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task RunHandlerUntilAsync(
        GrpcProtocolHandler handler,
        Func<WorkflowRequest, string, Task<WorkflowResponse>> workflowHandler,
        Func<ActivityRequest, string, Task<ActivityResponse>> activityHandler,
        Task until,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var runTask = handler.StartAsync(workflowHandler, activityHandler, cts.Token);
        try
        {
            await until.WaitAsync(cts.Token);
        }
        finally
        {
            // Always stop the infinite loop so the test can end
            cts.Cancel();
            await runTask;
        }
    }
    
    private static async Task RunHandlerUntilAsync(
        GrpcProtocolHandler handler,
        Func<WorkflowRequest, string, Task<WorkflowResponse>> workflowHandler,
        Func<ActivityRequest, string, Task<ActivityResponse>> activityHandler,
        Func<bool> untilCondition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(10);

        using var cts = new CancellationTokenSource(timeout);

        var runTask = handler.StartAsync(workflowHandler, activityHandler, cts.Token);

        try
        {
            var sw = Stopwatch.StartNew();

            while (!untilCondition())
            {
                cts.Token.ThrowIfCancellationRequested();

                // Cheap polling; avoids needing TCS in every test.
                await Task.Delay(pollInterval.Value, cts.Token);

                if (sw.Elapsed >= timeout)
                {
                    throw new TimeoutException($"Condition was not met within {timeout}.");
                }
            }
        }
        finally
        {
            cts.Cancel();
            await runTask;
        }
    }
    
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerFactoryIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() => new GrpcProtocolHandler(grpcClient, null!));
    }

    [Fact]
    public async Task StartAsync_ShouldCompleteOrchestratorTask_ForOrchestratorWorkItem()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                WorkflowRequest = new WorkflowRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<WorkflowResponse>();

        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback<WorkflowResponse, CallOptions>((r, _) => completedTcs.TrySetResult(r))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (req, _) => Task.FromResult(new WorkflowResponse { InstanceId = req.InstanceId }),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        var completed = await completedTcs.Task;
        Assert.Equal("i-1", completed.InstanceId);
    }

    [Fact]
    public async Task StartAsync_ShouldCompleteActivityTask_ForActivityWorkItem()
    {
        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 42,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-2" }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<ActivityResponse>();

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((r, _) => completedTcs.TrySetResult(r))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) => Task.FromResult(new ActivityResponse
            {
                InstanceId = req.WorkflowInstance.InstanceId,
                TaskId = req.TaskId,
                Result = "ok",
                CompletionToken = tok
            }),
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        var completed = await completedTcs.Task;
        Assert.Equal("i-2", completed.InstanceId);
        Assert.Equal(42, completed.TaskId);
        Assert.Equal("ok", completed.Result);
        Assert.Equal(completionToken, completed.CompletionToken);
    }

    [Fact]
    public async Task StartAsync_ShouldKeepActivityTraceCurrent_ThroughActivityCompletion()
    {
        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";
        const string expectedTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        const string traceParent = $"00-{expectedTraceId}-00f067aa0ba902b7-01";
        Activity.Current = null;

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 42,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-2" },
                    ParentTraceContext = new TraceContext { TraceParent = traceParent }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<(string? HandlerTraceId, string? CompletionTraceId)>();
        string? handlerTraceId = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((_, _) =>
                completedTcs.TrySetResult((handlerTraceId, Activity.Current?.TraceId.ToHexString())))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) =>
            {
                handlerTraceId = Activity.Current?.TraceId.ToHexString();
                return Task.FromResult(new ActivityResponse
                {
                    InstanceId = req.WorkflowInstance.InstanceId,
                    TaskId = req.TaskId,
                    Result = "ok",
                    CompletionToken = tok
                });
            },
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        var completed = await completedTcs.Task;
        Assert.Equal(expectedTraceId, completed.HandlerTraceId);
        Assert.Equal(expectedTraceId, completed.CompletionTraceId);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task StartAsync_ShouldPopulateActivityCurrent_FromParentTraceContext()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";
        const string expectedTraceId = "0af7651916cd43dd8448eb211c80319c";
        const string parentSpanId = "b7ad6b7169203331";
        const string traceParent = $"00-{expectedTraceId}-{parentSpanId}-01";
        const string traceState = "vendor=value";
        Activity.Current = null;

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 1,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "wf-1" },
                    ParentTraceContext = new TraceContext
                    {
                        TraceParent = traceParent,
                        TraceState = traceState
                    }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<bool>();
        Activity? observedCurrent = null;
        string? observedTraceId = null;
        string? observedParentSpanId = null;
        string? observedTraceState = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((_, _) => completedTcs.TrySetResult(true))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) =>
            {
                observedCurrent = Activity.Current;
                observedTraceId = Activity.Current?.TraceId.ToHexString();
                observedParentSpanId = Activity.Current?.ParentSpanId.ToHexString();
                observedTraceState = Activity.Current?.TraceStateString;
                return Task.FromResult(new ActivityResponse
                {
                    InstanceId = req.WorkflowInstance.InstanceId,
                    TaskId = req.TaskId,
                    Result = "ok",
                    CompletionToken = tok
                });
            },
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        Assert.NotNull(observedCurrent);
        Assert.Equal(expectedTraceId, observedTraceId);
        Assert.Equal(parentSpanId, observedParentSpanId);
        Assert.Equal(traceState, observedTraceState);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task StartAsync_ShouldPropagateTraceId_ToDownstreamActivities()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = _ => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        using var userSource = new ActivitySource("User.Code");

        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";
        const string expectedTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        const string traceParent = $"00-{expectedTraceId}-00f067aa0ba902b7-01";

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 2,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "wf-2" },
                    ParentTraceContext = new TraceContext { TraceParent = traceParent }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<bool>();
        string? downstreamTraceId = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((_, _) => completedTcs.TrySetResult(true))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) =>
            {
                using var downstream = userSource.StartActivity("downstream-http-call");
                downstreamTraceId = downstream?.TraceId.ToHexString();
                return Task.FromResult(new ActivityResponse
                {
                    InstanceId = req.WorkflowInstance.InstanceId,
                    TaskId = req.TaskId,
                    Result = "ok",
                    CompletionToken = tok
                });
            },
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(expectedTraceId, downstreamTraceId);
    }

    [Fact]
    public async Task StartAsync_ShouldLeaveActivityCurrentNull_WhenParentTraceContextIsMissing()
    {
        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";
        Activity.Current = null;

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 3,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "wf-3" }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<bool>();
        Activity? observedCurrent = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((_, _) => completedTcs.TrySetResult(true))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) =>
            {
                observedCurrent = Activity.Current;
                return Task.FromResult(new ActivityResponse
                {
                    InstanceId = req.WorkflowInstance.InstanceId,
                    TaskId = req.TaskId,
                    Result = "ok",
                    CompletionToken = tok
                });
            },
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Null(observedCurrent);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task StartAsync_ShouldFallBackToSetParentId_WhenTraceParentIsMalformed()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";
        const string malformedParentId = "not-a-valid-w3c-traceparent";
        Activity.Current = null;

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 4,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "wf-4" },
                    ParentTraceContext = new TraceContext { TraceParent = malformedParentId }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<bool>();
        string? observedParentId = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((_, _) => completedTcs.TrySetResult(true))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) =>
            {
                observedParentId = Activity.Current?.ParentId;
                return Task.FromResult(new ActivityResponse
                {
                    InstanceId = req.WorkflowInstance.InstanceId,
                    TaskId = req.TaskId,
                    Result = "ok",
                    CompletionToken = tok
                });
            },
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(malformedParentId, observedParentId);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task StartAsync_ShouldPopulateActivityCurrent_WithoutRegisteredListener()
    {
        var grpcClientMock = CreateGrpcClientMock();
        const string completionToken = "abc";
        const string expectedTraceId = "0af7651916cd43dd8448eb211c80319c";
        const string parentSpanId = "b7ad6b7169203331";
        const string traceParent = $"00-{expectedTraceId}-{parentSpanId}-01";
        const string traceState = "vendor=value";
        Activity.Current = null;

        var workItems = new[]
        {
            new WorkItem
            {
                ActivityRequest = new ActivityRequest
                {
                    Name = "act",
                    TaskId = 5,
                    WorkflowInstance = new WorkflowInstance { InstanceId = "wf-5" },
                    ParentTraceContext = new TraceContext
                    {
                        TraceParent = traceParent,
                        TraceState = traceState
                    }
                },
                CompletionToken = completionToken
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<bool>();
        Activity? observedCurrent = null;
        string? observedTraceId = null;
        string? observedParentSpanId = null;
        string? observedTraceState = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((_, _) => completedTcs.TrySetResult(true))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) =>
            {
                observedCurrent = Activity.Current;
                observedTraceId = Activity.Current?.TraceId.ToHexString();
                observedParentSpanId = Activity.Current?.ParentSpanId.ToHexString();
                observedTraceState = Activity.Current?.TraceStateString;
                return Task.FromResult(new ActivityResponse
                {
                    InstanceId = req.WorkflowInstance.InstanceId,
                    TaskId = req.TaskId,
                    Result = "ok",
                    CompletionToken = tok
                });
            },
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        Assert.NotNull(observedCurrent);
        Assert.Equal(expectedTraceId, observedTraceId);
        Assert.Equal(parentSpanId, observedParentSpanId);
        Assert.Equal(traceState, observedTraceState);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task StartAsync_ShouldSendFailureResult_WhenOrchestratorHandlerThrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                WorkflowRequest = new WorkflowRequest { InstanceId = "i-err" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedTcs = CreateTcs<WorkflowResponse>();

        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback<WorkflowResponse, CallOptions>((r, _) => completedTcs.TrySetResult(r))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => throw new InvalidOperationException("boom"),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            until: completedTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        var completed = await completedTcs.Task;

        Assert.Equal("i-err", completed.InstanceId);
        Assert.Single(completed.Actions);
        Assert.NotNull(completed.Actions[0].CompleteWorkflow);
        Assert.Equal(OrchestrationStatus.Failed, completed.Actions[0].CompleteWorkflow.WorkflowStatus);
        Assert.NotNull(completed.Actions[0].CompleteWorkflow.FailureDetails);
        Assert.Contains("boom", completed.Actions[0].CompleteWorkflow.FailureDetails.ErrorMessage);
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
    public async Task DisposeAsync_ShouldNotThrow_WhenCalledConcurrently()
    {
        var grpcClientMock = CreateGrpcClientMock();
        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        // Fire both calls simultaneously so they race through the idempotency guard.
        var t1 = handler.DisposeAsync().AsTask();
        var t2 = handler.DisposeAsync().AsTask();

        await Task.WhenAll(t1, t2);
    }
    
    [Fact]
    public async Task StartAsync_ShouldReturnWithoutThrowing_WhenGrpcStreamIsCancelled()
    {
        var grpcClientMock = CreateGrpcClientMock();

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Throws(new RpcException(new Status(StatusCode.Cancelled, "cancelled")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        // With "retry forever unless shutting down" semantics, the graceful shutdown signal
        // is cancellation. If the token is already canceled, StartAsync should exit immediately.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await handler.StartAsync(
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            cancellationToken: cts.Token);

        // Since we were already canceled, we shouldn't even attempt to connect.
        grpcClientMock.Verify(
            x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()),
            Times.Never());
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
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var sentTcs = CreateTcs<ActivityResponse>();

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((r, _) => sentTcs.TrySetResult(r))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => throw new InvalidOperationException("boom"),
            until: sentTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        var sent = await sentTcs.Task;

        Assert.Equal("i-1", sent.InstanceId);
        Assert.True(sent.TaskId > 0);
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
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completeAttempted = false;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback(() => completeAttempted = true)
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "nope")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => throw new Exception("boom"),
            untilCondition: () => completeAttempted,
            timeout: TimeSpan.FromSeconds(2));

        Assert.True(completeAttempted);
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
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var sentTcs = CreateTcs<ActivityResponse>();

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((r, _) => sentTcs.TrySetResult(r))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => throw new NullStackTraceException("boom"),
            until: sentTcs.Task,
            timeout: TimeSpan.FromSeconds(2));

        var sent = await sentTcs.Task;

        Assert.NotNull(sent.FailureDetails);
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
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCallIgnoringCancellation(workItems));

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        // Add a safety timeout so this test can never hang, even if behavior regresses.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await handler.StartAsync(
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) =>
            {
                // Make StartAsync's linked token "IsCancellationRequested == true"
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            cancellationToken: cts.Token);

        grpcClientMock.Verify(
            x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()),
            Times.Never());
    }

    [Fact]
    public async Task StartAsync_ShouldNotCallCompleteOrchestratorTask_WhenWorkflowHandlerThrowsOperationCanceledException_AndTokenIsCanceled()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                WorkflowRequest = new WorkflowRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCallIgnoringCancellation(workItems));

        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        // Add a safety timeout so this test can never hang, even if behavior regresses.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await handler.StartAsync(
            workflowHandler: (_,_) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            cancellationToken: cts.Token);

        grpcClientMock.Verify(
            x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()),
            Times.Never());
    }

    [Fact]
    public async Task StartAsync_ShouldCleanupCompletedActiveWorkItems_WhenActiveWorkItemsListGrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        // With maxConcurrentWorkItems=1, cleanup threshold is > 2.
        // We send 3 work items that complete quickly so RemoveAll(t => t.IsCompleted) is executed.
        var workItems = new[]
        {
            new WorkItem { ActivityRequest = new ActivityRequest { Name = "a1", TaskId = 1, WorkflowInstance = new WorkflowInstance { InstanceId = "i" } } },
            new WorkItem { ActivityRequest = new ActivityRequest { Name = "a2", TaskId = 2, WorkflowInstance = new WorkflowInstance { InstanceId = "i" } } },
            new WorkItem { ActivityRequest = new ActivityRequest { Name = "a3", TaskId = 3, WorkflowInstance = new WorkflowInstance { InstanceId = "i" } } },
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completedCount = 0;
        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback(() => Interlocked.Increment(ref completedCount))
            .Returns(CreateAsyncUnaryCall(new CompleteTaskResponse()));

        var handler = new GrpcProtocolHandler(
            grpcClientMock.Object,
            NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, _) => Task.FromResult(new ActivityResponse
            {
                InstanceId = req.WorkflowInstance.InstanceId,
                TaskId = req.TaskId,
                Result = "ok"
            }),
            untilCondition: () => Volatile.Read(ref completedCount) >= 3,
            timeout: TimeSpan.FromSeconds(2));

        Assert.Equal(3, Volatile.Read(ref completedCount));
    }
    
    [Fact]
    public async Task StartAsync_ShouldNotThrow_WhenWorkflowHandlerThrows_AndSendingFailureResultAlsoThrows()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                WorkflowRequest = new WorkflowRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completeAttempted = false;

        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback(() => completeAttempted = true)
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "nope")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => throw new InvalidOperationException("boom"),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            untilCondition: () => completeAttempted,
            timeout: TimeSpan.FromSeconds(2));

        Assert.True(completeAttempted);
    }

    [Fact]
    public async Task StartAsync_ShouldHandleUnknownWorkItemType_AndWaitForActiveTasks()
    {
        var grpcClientMock = CreateGrpcClientMock();

        WorkItem[] workItems =
        [
            new() // RequestCase = None
        ];

        var getWorkItemsCalled = false;

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => getWorkItemsCalled = true)
            .Returns(CreateServerStreamingCall(workItems));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            untilCondition: () => getWorkItemsCalled,
            timeout: TimeSpan.FromSeconds(2));

        Assert.True(getWorkItemsCalled);
    }
    
    [Fact]
    public async Task StartAsync_ShouldRethrow_WhenReceiveLoopThrowsBeforeAnyItemsAreRead()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var getWorkItemsCalls = 0;

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => Interlocked.Increment(ref getWorkItemsCalls))
            .Returns(CreateServerStreamingCallFromReader(
                new ThrowingAsyncStreamReader(new InvalidOperationException("boom"))));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            untilCondition: () => Volatile.Read(ref getWorkItemsCalls) >= 1,
            timeout: TimeSpan.FromSeconds(2));

        Assert.True(Volatile.Read(ref getWorkItemsCalls) >= 1);
    }

    [Fact]
    public async Task StartAsync_ShouldRethrow_WhenReceiveLoopThrowsAfterFirstItemIsRead()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var reader = new ThrowingAfterOneAsyncStreamReader(
            first: new WorkItem(), // RequestCase = None => Task.Run branch
            thenThrow: new InvalidOperationException("boom-after-one"));

        var getWorkItemsCalls = 0;

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => Interlocked.Increment(ref getWorkItemsCalls))
            .Returns(CreateServerStreamingCallFromReader(reader));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        // With "retry forever unless shutting down" semantics and a 5s reconnect delay,
        // we should *not* expect a second call within a 2s test timeout.
        // Instead, assert that the handler attempted to read from the stream (at least once)
        // and can be canceled cleanly by the test harness.
        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_,_) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_,_) => Task.FromResult(new ActivityResponse()),
            untilCondition: () => Volatile.Read(ref getWorkItemsCalls) >= 1,
            timeout: TimeSpan.FromSeconds(2));

        Assert.True(Volatile.Read(ref getWorkItemsCalls) >= 1);
    }

    [Fact]
    public async Task StartAsync_ShouldNotLogException_WhenReceiveLoopCancellationIsRequested()
    {
        var grpcClientMock = CreateGrpcClientMock();
        using var loggerFactory = new CapturingLoggerFactory();

        var loggedCancellation = CreateTcs<LogEntry>();

        loggerFactory.Logged += entry =>
        {
            if (entry.Message == "Workflow worker gRPC stream canceled during shutdown (expected)")
            {
                loggedCancellation.TrySetResult(entry);
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(() => CreateServerStreamingCallFromReader(new CancelledWhenTokenCanceledStreamReader()));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, loggerFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var runTask = handler.StartAsync(
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_, _) => Task.FromResult(new ActivityResponse()),
            cancellationToken: cts.Token);

        await Task.Delay(50, cts.Token);
        cts.Cancel();

        await runTask;

        var cancellationLog = await loggedCancellation.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(LogLevel.Information, cancellationLog.Level);
        Assert.Null(cancellationLog.Exception);
    }

    [Fact]
    public async Task StartAsync_ShouldLogException_WhenReceiveLoopCancellationIsUnexpected()
    {
        var grpcClientMock = CreateGrpcClientMock();
        using var loggerFactory = new CapturingLoggerFactory();

        var loggedUnexpectedCancellation = CreateTcs<LogEntry>();

        loggerFactory.Logged += entry =>
        {
            if (entry.Message == "Error in receive loop" && entry.Exception is RpcException { StatusCode: StatusCode.Cancelled })
            {
                loggedUnexpectedCancellation.TrySetResult(entry);
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCallFromReader(
                new ThrowingAsyncStreamReader(new RpcException(new Status(StatusCode.Cancelled, "unexpected")))));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, loggerFactory);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (_, _) => Task.FromResult(new ActivityResponse()),
            until: loggedUnexpectedCancellation.Task,
            timeout: TimeSpan.FromSeconds(2));

        var cancellationLog = await loggedUnexpectedCancellation.Task;
        Assert.Equal(LogLevel.Error, cancellationLog.Level);
        Assert.NotNull(cancellationLog.Exception);
    }

    /// <summary>
    /// Regression test for: "Task failed: Status(StatusCode="Unknown", Detail="no such instance exists")"
    ///
    /// Root cause: the old code had a single try/catch that wrapped both the handler execution and
    /// the CompleteActivityTaskAsync delivery call. When the delivery call threw an RpcException
    /// (e.g. "no such instance exists" — a transient sidecar condition), the exception was caught and
    /// a *secondary* CompleteActivityTaskAsync call was made carrying FailureDetails. The sidecar
    /// recorded this as a TaskFailed event. On the next workflow replay, HandleFailedActivityFromHistory
    /// threw WorkflowTaskFailedException("Task failed: Status(StatusCode=Unknown, Detail=no such
    /// instance exists)"), propagating the transport error as a business-logic failure.
    ///
    /// Fix: the delivery call is now in its own try/catch so a transport failure is logged and
    /// abandoned — it does NOT produce a secondary failure response.
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldCallCompleteActivityTaskOnce_WhenActivitySucceeds_AndCompleteActivityTaskThrowsRpcException()
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
                    WorkflowInstance = new WorkflowInstance { InstanceId = "i-1" }
                }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completeCallCount = 0;
        ActivityResponse? capturedResponse = null;

        grpcClientMock
            .Setup(x => x.CompleteActivityTaskAsync(It.IsAny<ActivityResponse>(), It.IsAny<CallOptions>()))
            .Callback<ActivityResponse, CallOptions>((r, _) =>
            {
                Interlocked.Increment(ref completeCallCount);
                capturedResponse = r;
            })
            .Throws(new RpcException(new Status(StatusCode.Unknown, "no such instance exists")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (_, _) => Task.FromResult(new WorkflowResponse()),
            activityHandler: (req, tok) => Task.FromResult(new ActivityResponse
            {
                InstanceId = req.WorkflowInstance.InstanceId,
                TaskId = req.TaskId,
                Result = "success",
                CompletionToken = tok
            }),
            untilCondition: () => Volatile.Read(ref completeCallCount) >= 1,
            timeout: TimeSpan.FromSeconds(2));

        // CompleteActivityTaskAsync must be called exactly once — with the success result.
        // The old code would call it a second time with FailureDetails set, causing a TaskFailed
        // history event that propagates the transport error as a workflow-level activity failure.
        Assert.Equal(1, Volatile.Read(ref completeCallCount));
        Assert.NotNull(capturedResponse);
        Assert.Null(capturedResponse!.FailureDetails); // success result, no failure details
        Assert.Equal("success", capturedResponse.Result);
    }

    /// <summary>
    /// Regression test for the orchestrator path of the same bug. When a workflow turn completes
    /// successfully but CompleteOrchestratorTaskAsync fails transiently, the handler must log and
    /// abandon rather than sending a secondary failure response that would corrupt workflow history.
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldCallCompleteOrchestratorTaskOnce_WhenWorkflowHandlerSucceeds_AndCompleteOrchestratorTaskThrowsRpcException()
    {
        var grpcClientMock = CreateGrpcClientMock();

        var workItems = new[]
        {
            new WorkItem
            {
                WorkflowRequest = new WorkflowRequest { InstanceId = "i-1" }
            }
        };

        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateServerStreamingCall(workItems));

        var completeCallCount = 0;
        WorkflowResponse? capturedResponse = null;

        grpcClientMock
            .Setup(x => x.CompleteOrchestratorTaskAsync(It.IsAny<WorkflowResponse>(), It.IsAny<CallOptions>()))
            .Callback<WorkflowResponse, CallOptions>((r, _) =>
            {
                Interlocked.Increment(ref completeCallCount);
                capturedResponse = r;
            })
            .Throws(new RpcException(new Status(StatusCode.Unknown, "no such instance exists")));

        var handler = new GrpcProtocolHandler(grpcClientMock.Object, NullLoggerFactory.Instance);

        await RunHandlerUntilAsync(
            handler,
            workflowHandler: (req, _) => Task.FromResult(new WorkflowResponse { InstanceId = req.InstanceId }),
            activityHandler: (_, _) => Task.FromResult(new ActivityResponse()),
            untilCondition: () => Volatile.Read(ref completeCallCount) >= 1,
            timeout: TimeSpan.FromSeconds(2));

        // CompleteOrchestratorTaskAsync must be called exactly once — with the success result.
        // The old code would call it a second time with OrchestrationStatus.Failed, corrupting history.
        Assert.Equal(1, Volatile.Read(ref completeCallCount));
        Assert.NotNull(capturedResponse);
        Assert.DoesNotContain(capturedResponse!.Actions,
            a => a.CompleteWorkflow?.WorkflowStatus == OrchestrationStatus.Failed);
    }

    [Fact]
    public async Task DelayOrStopAsync_ShouldSwallowCancellation_WhenTokenIsCanceled()
    {
        var method = typeof(GrpcProtocolHandler).GetMethod("DelayOrStopAsync", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = (Task)method.Invoke(null, [TimeSpan.FromMilliseconds(1), cts.Token])!;
        await task;
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

    private sealed class CancelledWhenTokenCanceledStreamReader : IAsyncStreamReader<WorkItem>
    {
        public WorkItem Current => new();

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return false;
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

    private sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public event Action<LogEntry>? Logged;

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Capture);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private void Capture(LogEntry entry)
        {
            _entries.Enqueue(entry);
            Logged?.Invoke(entry);
        }

        private sealed class CapturingLogger(string categoryName, Action<LogEntry> capture) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                capture(new LogEntry(logLevel, categoryName, formatter(state, exception), exception));
            }
        }
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
