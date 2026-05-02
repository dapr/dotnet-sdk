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
using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker;
using Dapr.Workflow.Worker.Grpc;
using Dapr.Workflow.Worker.Internal;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Type = System.Type;

namespace Dapr.Workflow.Test.Worker;

public class WorkflowWorkerTests
{
    [Fact]
    public async Task HandleActivityResponseAsync_ShouldPopulateActivityCurrent_FromParentTraceContext()
    {
        // Force ActivitySource.StartActivity to actually create Activities
        // by attaching a sampler that always records.
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        // W3C traceparent: version-traceId-spanId-flags
        const string expectedTraceId = "0af7651916cd43dd8448eb211c80319c";
        const string parentSpanId = "b7ad6b7169203331";
        const string traceParent = $"00-{expectedTraceId}-{parentSpanId}-01";
        const string traceState = "vendor=value";

        Activity? observedCurrent = null;
        string? observedTraceId = null;
        string? observedParentSpanId = null;
        string? observedTraceState = null;

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) =>
            {
                // User reports in #1749 that Activity.Current is null
                // After fix, this should be non-null in the activity body
                observedCurrent = Activity.Current;
                observedTraceId = Activity.Current?.TraceId.ToHexString();
                observedParentSpanId = Activity.Current?.ParentSpanId.ToHexString();
                observedTraceState = Activity.Current?.TraceStateString;
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 1,
            Input = string.Empty,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "wf-1" },
            ParentTraceContext = new TraceContext
            {
                TraceParent = traceParent,
                TraceState = traceState
            }
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Null(response.FailureDetails);
        Assert.NotNull(observedCurrent);
        Assert.Equal(expectedTraceId, observedTraceId);
        Assert.Equal(parentSpanId, observedParentSpanId);
        Assert.Equal(traceState, observedTraceState);

        // And, critically: after the activity returns, the ambient activity should have
        // been restored (disposed) so we don't leak state onto the worker thread.
        Assert.NotEqual(observedCurrent, Activity.Current);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldPropagateTraceId_ToDownstreamActivities()
    {
        // This is the actual user-visible symptom from issue #1749: downstream calls
        // appearing under a different TraceId. Here we simulate a downstream
        // ActivitySource.StartActivity call and assert that the TraceId matches the
        // traceparent supplied by the sidecar.
        using var listener = new ActivityListener();
        listener.ShouldListenTo = _ => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        using var userSource = new ActivitySource("User.Code");

        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        const string expectedTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        const string parentSpanId = "00f067aa0ba902b7";
        const string traceParent = $"00-{expectedTraceId}-{parentSpanId}-01";

        string? downstreamTraceId = null;

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) =>
            {
                using var downstream = userSource.StartActivity("downstream-http-call");
                downstreamTraceId = downstream?.TraceId.ToHexString();
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 2,
            Input = string.Empty,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "wf-2" },
            ParentTraceContext = new TraceContext { TraceParent = traceParent }
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Null(response.FailureDetails);
        Assert.Equal(expectedTraceId, downstreamTraceId);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldLeaveActivityCurrentNull_WhenParentTraceContextIsMissing()
    {
        // Behavior must be unchanged (no ambient Activity) when the sidecar didn't
        // supply a traceparent — the fix must not start spurious Activities.
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        Activity? observedCurrent = null;

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) =>
            {
                observedCurrent = Activity.Current;
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // Ensure the surrounding test runner hasn't left an ambient activity on this
        // async context that could mask a regression.
        Activity.Current = null;

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 3,
            Input = string.Empty,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "wf-3" }
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Null(response.FailureDetails);
        Assert.Null(observedCurrent);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldFallBackToSetParentId_WhenTraceParentIsMalformed()
    {
        // Covers the fallback branch in StartActivityFromRequest: a non-W3C parent id
        // still yields a non-null Activity.Current whose raw ParentId matches input.
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);

        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        const string malformedParentId = "not-a-valid-w3c-traceparent";

        string? observedParentId = null;

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) =>
            {
                observedParentId = Activity.Current?.ParentId;
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 4,
            Input = string.Empty,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "wf-4" },
            ParentTraceContext = new TraceContext { TraceParent = malformedParentId }
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Null(response.FailureDetails);
        Assert.Equal(malformedParentId, observedParentId);
    }
    
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenGrpcClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(null!, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenWorkflowsFactoryIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, null!, Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerFactoryIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), null!, Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSerializerIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), null!,
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceProviderIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                null!, new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), null!));
    }

    [Fact]
    public async Task StopAsync_ShouldNotThrow_WhenProtocolHandlerWasNeverCreated()
    {
        var grpcClient = CreateGrpcClientMock().Object;
        var worker = new WorkflowWorker(
            grpcClient,
            Mock.Of<IWorkflowsFactory>(),
            NullLoggerFactory.Instance,
            Mock.Of<IWorkflowSerializer>(),
            new ServiceCollection().BuildServiceProvider(),
            new WorkflowRuntimeOptions());

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeProtocolHandler_WhenPresent()
    {
        var grpcClient = CreateGrpcClientMock().Object;
        var worker = new WorkflowWorker(
            grpcClient,
            Mock.Of<IWorkflowsFactory>(),
            NullLoggerFactory.Instance,
            Mock.Of<IWorkflowSerializer>(),
            new ServiceCollection().BuildServiceProvider(),
            new WorkflowRuntimeOptions());

        var protocolHandler = new GrpcProtocolHandler(CreateGrpcClientMock().Object, NullLoggerFactory.Instance, 1, 1);

        var field = typeof(WorkflowWorker).GetField("_protocolHandler", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(worker, protocolHandler);

        await worker.StopAsync(CancellationToken.None);
    }
    
    [Fact]
    public async Task ExecuteAsync_ShouldComplete_WhenGrpcStreamCompletesImmediately()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();

        var startedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => startedTcs.TrySetResult())
            .Returns(CreateServerStreamingCall(EmptyWorkItems()));

        var worker = new WorkflowWorker(
            grpcClientMock.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            services,
            options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var executeTask = InvokeExecuteAsync(worker, cts.Token);

        // Wait until the worker actually tries to connect, then stop it cleanly.
        await startedTcs.Task.WaitAsync(cts.Token);
        cts.Cancel();

        await executeTask;
    }
    
    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnTerminatedCompletion_WhenReplayLatestEventIsExecutionTerminated()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        // Intentionally no workflow registrations: this verifies the termination path
        // is acknowledged before workflow lookup/instantiation.
        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf-not-registered", Input = "" }
                }
            },
            NewEvents =
            {
                new HistoryEvent
                {
                    ExecutionTerminated = new ExecutionTerminatedEvent()
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var action = Assert.Single(response.Actions);
        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.Terminated, action.CompleteOrchestration!.OrchestrationStatus);
    }
    
    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldNotReturnTerminatedCompletion_WhenReplayLatestEventIsNotExecutionTerminated()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        // Intentionally no workflow registrations. If the termination short-circuit does NOT trigger,
        // normal path should fail with WorkflowNotFound-style completion.
        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf-not-registered", Input = "" }
                }
            },
            NewEvents =
            {
                new HistoryEvent
                {
                    ExecutionTerminated = new ExecutionTerminatedEvent()
                },
                new HistoryEvent
                {
                    OrchestratorStarted = new OrchestratorStartedEvent()
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var action = Assert.Single(response.Actions);
        Assert.NotNull(action.CompleteOrchestration);
        Assert.NotEqual(OrchestrationStatus.Terminated, action.CompleteOrchestration!.OrchestrationStatus);
        Assert.Equal(OrchestrationStatus.Failed, action.CompleteOrchestration.OrchestrationStatus);
    }
    
    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnEmptyResponse_WhenLatestEventIsExecutionSuspended()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf-not-registered", Input = "" }
                }
            },
            NewEvents =
            {
                new HistoryEvent
                {
                    ExecutionSuspended = new ExecutionSuspendedEvent()
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Empty(response.Actions);
    }
    
    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldNotShortCircuit_WhenLatestEventIsExecutionResumed()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf-not-registered", Input = "" }
                }
            },
            NewEvents =
            {
                new HistoryEvent
                {
                    ExecutionResumed = new ExecutionResumedEvent()
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var action = Assert.Single(response.Actions);
        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.Failed, action.CompleteOrchestration!.OrchestrationStatus);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSwallowOperationCanceledException_WhenStoppingTokenIsCanceled()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Returns((GetWorkItemsRequest _, CallOptions opt) =>
            {
                opt.CancellationToken.ThrowIfCancellationRequested();
                return CreateServerStreamingCall(EmptyWorkItems());
            });

        var worker = new WorkflowWorker(
            grpcClientMock.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            services,
            options);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await InvokeExecuteAsync(worker, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrow_WhenOptionsHaveInvalidConcurrency()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        // Bypass property validation to simulate corrupted configuration.
        typeof(WorkflowRuntimeOptions)
            .GetField("_maxConcurrentWorkflows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(options, 0);

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            services,
            options);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => InvokeExecuteAsync(worker, CancellationToken.None));
    }

    [Fact]
    public void CreateCallOptions_ShouldIncludeUserAgentAndApiToken_WhenConfigured()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_API_TOKEN"] = "workflow-token"
            })
            .Build();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            services,
            options,
            configuration);

        using var cts = new CancellationTokenSource();
        var callOptions = InvokeCreateCallOptions(worker, cts.Token);

        Assert.Equal(cts.Token, callOptions.CancellationToken);
        Assert.True(HasHeader(callOptions, "User-Agent", out var userAgent));
        Assert.Contains("dapr-sdk-dotnet", userAgent, StringComparison.OrdinalIgnoreCase);
        Assert.True(HasHeader(callOptions, "dapr-api-token", out var tokenValue));
        Assert.Equal("workflow-token", tokenValue);
    }

    [Fact]
    public void CreateCallOptions_ShouldNotIncludeApiTokenHeader_WhenTokenIsEmpty()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_API_TOKEN"] = ""
            })
            .Build();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            services,
            options,
            configuration);

        var callOptions = InvokeCreateCallOptions(worker, CancellationToken.None);

        Assert.False(HasHeader(callOptions, "dapr-api-token", out _));
        Assert.True(HasHeader(callOptions, "User-Agent", out _));
    }
    
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldComplete_WhenCompletionEventArrivesLater()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance, new WorkflowVersionTracker([]));

        var task = context.CallChildWorkflowAsync<int>("ChildWf");

        var creationHistory = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent
                {
                    Name = "ChildWf"
                }
            }
        };

        context.ProcessEvents(creationHistory, true);

        Assert.False(task.IsCompleted);

        var completionHistory = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 0,
                    Result = "99"
                }
            }
        };

        context.ProcessEvents(completionHistory, false);
        var value = await task;

        Assert.Equal(99, value);
        Assert.Empty(context.PendingActions);
    }
    
    [Fact]
    public void CallChildWorkflowAsync_ShouldPreserveRouterTargetAppId_OnScheduledAction()
    {
        const string appId1 = "this-app";
        const string appId2 = "remote-app";
        
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            "wf", "parent", new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            serializer, NullLoggerFactory.Instance, new WorkflowVersionTracker([]), appId1);

        _ = context.CallChildWorkflowAsync<int>("ChildWf", options: new ChildWorkflowTaskOptions { TargetAppId = appId2 });

        var action = Assert.Single(context.PendingActions);
        Assert.NotNull(action.CreateSubOrchestration);
        Assert.NotNull(action.Router);
        Assert.Equal(appId1, action.Router.SourceAppID);
        Assert.Equal(appId2, action.Router.TargetAppID);
    }
    
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldComplete_WhenCompletionArrivedBeforeCall()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            "wf", "parent", new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            serializer, NullLoggerFactory.Instance, new WorkflowVersionTracker([]));

        var completionEvent = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 0,
                    Result = "13"
                }
            }
        };

        // Completion arrives before the call; nothing happens yet.
        context.ProcessEvents(completionEvent, false);

        var task = context.CallChildWorkflowAsync<int>("ChildWf");

        context.ProcessEvents([
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            }
        ], false);

        // Replay the completion now that the task exists.
        context.ProcessEvents(completionEvent, false);

        var value = await task;
        Assert.Equal(13, value);
        Assert.Empty(context.PendingActions);
    }
    
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldIgnoreDuplicateCompletionEvents()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            "wf", "parent", new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            serializer, NullLoggerFactory.Instance, new WorkflowVersionTracker([]));

        var task = context.CallChildWorkflowAsync<int>("ChildWf");

        context.ProcessEvents([
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            }
        ], true);

        context.ProcessEvents([
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 999,
                    Result = "100"
                }
            },
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 0,
                    Result = "200"
                }
            }
        ], false);

        var value = await task;
        Assert.Equal(200, value);
    }
    
    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldAllowWorkflowToComplete_OnSecondPass_WhenChildCompletionInHistory()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();

        // Workflow: await a child workflow, then return null (output ignored here)
        factory.AddWorkflow("InitialWorkflow", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.CallChildWorkflowAsync<int>("TargetWorkflow", input: 7,
                    options: new ChildWorkflowTaskOptions(InstanceId: "remote-workflow-instance", TargetAppId: "workflow-app-2"));
                return null;
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // Pass 1: only ExecutionStarted, so it should schedule CreateSubOrchestration and yield (not completed)
        var pass1 = new OrchestratorRequest
        {
            InstanceId = "initial-workflow-instance",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "InitialWorkflow", Input = "" }
                }
            }
        };

        var resp1 = await InvokeHandleOrchestratorResponseAsync(worker, pass1);
        Assert.Contains(resp1.Actions, a => a.CreateSubOrchestration != null);
        Assert.DoesNotContain(resp1.Actions, a => a.CompleteOrchestration != null);

        // Pass 2: include sub-orchestration completed with taskScheduledId=0
        var pass2 = new OrchestratorRequest
        {
            InstanceId = "initial-workflow-instance",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "InitialWorkflow", Input = "" }
                },
                new HistoryEvent
                {
                    SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent
                    {
                        InstanceId = "remote-workflow-instance",
                        Name = "TargetWorkflow",
                        Input = "7"
                    }
                },
                new HistoryEvent
                {
                    SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                    {
                        TaskScheduledId = 0,
                        Result = "21"
                    }
                }
            }
        };

        var resp2 = await InvokeHandleOrchestratorResponseAsync(worker, pass2);
        Assert.Contains(resp2.Actions, a => a.CompleteOrchestration != null);
        Assert.Equal(OrchestrationStatus.Completed, resp2.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!.OrchestrationStatus);
    }
    
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldOnlyCompleteAfterCreation_WhenCompletionArrivesFirst()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            "wf", "parent", new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            serializer, NullLoggerFactory.Instance, new WorkflowVersionTracker([]));

        var completionHistory = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 0,
                    Result = "21"
                }
            }
        };

        context.ProcessEvents(completionHistory, false);
        var task = context.CallChildWorkflowAsync<int>("ChildWf");

        context.ProcessEvents([
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            }
        ], false);

        var value = await task;
        Assert.Equal(21, value);
    }
    
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldCompleteOnlyForMatchingTaskScheduledId_WhenReplaySchedulesAgain()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            "wf", "parent", new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            serializer, NullLoggerFactory.Instance, new WorkflowVersionTracker([]));

        var task = context.CallChildWorkflowAsync<int>("ChildWf");

        var historyFirstCreation = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            }
        };

        context.ProcessEvents(historyFirstCreation, true);

        Assert.False(task.IsCompleted);

        var historyReplayScheduling = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            }
        };

        context.ProcessEvents(historyReplayScheduling, false);

        var completionHistory = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 0,
                    Result = "7"
                }
            }
        };

        context.ProcessEvents(completionHistory, false);

        var value = await task;
        Assert.Equal(7, value);
        Assert.Empty(context.PendingActions);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnEmptyActions_WhenWorkflowNameMissingInHistory()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents = { new HistoryEvent { TimerFired = new TimerFiredEvent() } }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var action = Assert.Single(response.Actions);
        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.Failed, action.CompleteOrchestration.OrchestrationStatus);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnEmptyActions_WhenWorkflowNotInRegistry()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(), // no registrations
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "123" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var action = Assert.Single(response.Actions);
        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.Failed, action.CompleteOrchestration.OrchestrationStatus);
        Assert.Equal("WorkflowNotFound", action.CompleteOrchestration.FailureDetails.ErrorType);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnActivationFailure_WhenWorkflowActivationFails()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflowActivationError("wf", new InvalidOperationException("No service for type 'IMyService' has been registered."));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "123" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var activationAction = Assert.Single(response.Actions);
        Assert.NotNull(activationAction.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.Failed, activationAction.CompleteOrchestration.OrchestrationStatus);
        Assert.NotEqual("WorkflowNotFound", activationAction.CompleteOrchestration.FailureDetails.ErrorType);
        Assert.Contains("failed to activate", activationAction.CompleteOrchestration.FailureDetails.ErrorMessage);
        Assert.Contains("IMyService", activationAction.CompleteOrchestration.FailureDetails.ErrorMessage);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldCompleteWorkflow_AndIncludeOutputAndCustomStatus()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: (ctx, input) =>
            {
                ctx.SetCustomStatus(new { Step = 7 });
                return Task.FromResult<object?>((int)input! + 1);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "41" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Contains("\"step\":7", response.CustomStatus);

        var completion = response.Actions
            .FirstOrDefault(a => a.CompleteOrchestration != null)?.CompleteOrchestration;
        
        Assert.NotNull(completion);
        Assert.Equal(OrchestrationStatus.Completed, completion.OrchestrationStatus);
        Assert.Equal("42", completion.Result);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldNotAddCompletedAction_WhenWorkflowContinuesAsNew()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(string),
            run: (ctx, _) =>
            {
                ctx.ContinueAsNew(new { Next = "x" }, preserveUnprocessedEvents: true);
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "\"in\"" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);

        var completeActions = response.Actions.Where(a => a.CompleteOrchestration != null).ToList();
        Assert.Single(completeActions);
        Assert.Equal(OrchestrationStatus.ContinuedAsNew, completeActions[0].CompleteOrchestration!.OrchestrationStatus);

        Assert.DoesNotContain(response.Actions,
            a => a.CompleteOrchestration?.OrchestrationStatus == OrchestrationStatus.Completed);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnFailedCompletion_WhenWorkflowThrows()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: (_, _) => throw new InvalidOperationException("boom")));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "1" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);

        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Failed, complete.OrchestrationStatus);
        Assert.NotNull(complete.FailureDetails);
        Assert.Contains("boom", complete.FailureDetails.ErrorMessage);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldReturnNotFoundFailure_WhenActivityNotInRegistry()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 7,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" },
            Input = "1"
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Equal(7, response.TaskId);
        Assert.NotNull(response.FailureDetails);
        Assert.Equal("ActivityNotFoundException", response.FailureDetails.ErrorType);
        Assert.Contains("Activity 'act' not found", response.FailureDetails.ErrorMessage);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldReturnActivationFailure_WhenActivityActivationFails()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivityActivationError("act", new InvalidOperationException("No service for type 'IEmailSender' has been registered."));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 7,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" },
            Input = "1"
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Equal(7, response.TaskId);
        Assert.NotNull(response.FailureDetails);
        Assert.NotEqual("ActivityNotFoundException", response.FailureDetails.ErrorType);
        Assert.Contains("failed to activate", response.FailureDetails.ErrorMessage);
        Assert.Contains("IEmailSender", response.FailureDetails.ErrorMessage);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldExecuteActivity_AndSerializeResult()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: async (_, input) =>
            {
                await Task.Yield();
                return (int)input! * 2;
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 7,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" },
            Input = "21"
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Equal(7, response.TaskId);
        Assert.Null(response.FailureDetails);
        Assert.Equal("42", response.Result);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldReturnFailureDetails_WhenActivityThrows()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) => throw new InvalidOperationException("boom")));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 7,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" },
            Input = "1"
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Equal(7, response.TaskId);
        Assert.NotNull(response.FailureDetails);
        Assert.Contains("boom", response.FailureDetails.ErrorMessage);
    }
    
    [Fact]
    public async Task ExecuteAsync_ShouldRetry_WhenGrpcProtocolHandlerStartFailsWithException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();

        var attemptedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => attemptedTcs.TrySetResult())
            .Throws(new InvalidOperationException("boom"));

        var worker = new WorkflowWorker(
            grpcClientMock.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            services,
            options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var executeTask = InvokeExecuteAsync(worker, cts.Token);

        // Wait until we observe at least one attempt, then stop the worker.
        await attemptedTcs.Task.WaitAsync(cts.Token);
        cts.Cancel();

        await executeTask;

        grpcClientMock.Verify(
            x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), It.IsAny<CallOptions>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldUseFirstEventTimestamp_WhenPresent_AndSerializeEmptyResult_WhenOutputIsNull()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: (ctx, input) =>
            {
                // Exercise the timestamp-based CurrentUtcDateTime path via deterministic GUID generation
                // (no assertion on GUID value needed; just cover the code path safely).
                _ = ctx.NewGuid();

                // Return null output -> worker should serialize to empty string in completion action.
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                        new DateTime(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc)),
                    ExecutionStarted = new ExecutionStartedEvent
                    {
                        Name = "wf",
                        Input = "123"
                    }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.Null(response.CustomStatus);

        var complete = response.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!;
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);
        Assert.Equal(string.Empty, complete.Result);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldAdvanceCurrentUtcDateTime_WhenTimerFires()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();
        var beginDateTime = new DateTime(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc);

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: async (ctx, _) =>
            {
                Assert.Equal(beginDateTime, ctx.CurrentUtcDateTime);
                await ctx.CreateTimer(TimeSpan.FromSeconds(5));
                Assert.Equal(beginDateTime.AddSeconds(5), ctx.CurrentUtcDateTime);
                
                return null;
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime),
                    ExecutionStarted = new ExecutionStartedEvent
                    {
                        Name = "wf",
                        Input = "123"
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5))
                    }
                },
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5)),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                },
                new HistoryEvent
                {
                    TimerFired = new TimerFiredEvent
                    {
                        TimerId = 0,
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5))
                    }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!;
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);
        Assert.Equal(string.Empty, complete.Result);
    }

    /// <summary>
    /// Regression test: CurrentUtcDateTime must equal the workflow's initial start time before the first
    /// await on every replay, not the current turn's timestamp.
    ///
    /// The bug: WorkflowWorker initialised _currentUtcDateTime with the *current turn's*
    /// OrchestratorStarted timestamp (T3) instead of the *first* history event's timestamp (T1).
    /// The workflow code ran before ProcessEvents and read the wrong time.
    /// </summary>
    [Fact]
    public async Task HandleOrchestratorResponseAsync_CurrentUtcDateTime_IsConsistentBeforeFirstAwait_OnReplay()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var t1 = new DateTime(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc); // workflow started
        var t2 = t1.AddSeconds(5);                                         // activity completed
        var t3 = t2.AddSeconds(5);                                         // current turn start

        DateTime capturedBeforeAwait = default;
        DateTime capturedAfterActivityAwait = default;

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                capturedBeforeAwait = ctx.CurrentUtcDateTime; // must equal T1 on every replay
                await ctx.CallActivityAsync<string>("act");
                capturedAfterActivityAwait = ctx.CurrentUtcDateTime; // must equal T2
                return null;
            }));
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>("result")));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // Simulate a replay turn: PastEvents contain the first turn's history (activity scheduled
        // and completed), NewEvents hold the current turn's OrchestratorStarted at the later time T3.
        // Before the fix, CurrentUtcDateTime before the first await would be T3, not T1.
        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(t1),
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf" }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TaskScheduled = new TaskScheduledEvent { Name = "act" }
                },
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(t2),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                },
                new HistoryEvent
                {
                    TaskCompleted = new TaskCompletedEvent
                    {
                        TaskScheduledId = 0,
                        Result = "\"result\""
                    }
                }
            },
            NewEvents =
            {
                // Current turn starts at T3 — this is what the bug incorrectly used as
                // the initial CurrentUtcDateTime before any workflow code ran.
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(t3),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!;
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);

        // Before the fix this was T3 (the current turn's timestamp). It must be T1 so that
        // the value the workflow observes before its first await is consistent across replays.
        Assert.Equal(t1, capturedBeforeAwait);

        // After the activity completes the clock should have advanced to T2, as recorded
        // by the OrchestratorStarted event that preceded the TaskCompleted event.
        Assert.Equal(t2, capturedAfterActivityAwait);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldCompleted_WhenEventReceived()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();
        var beginDateTime = new DateTime(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc);

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: async (ctx, _) =>
            {
                await ctx.WaitForExternalEventAsync<object>("MyEvent", TimeSpan.FromSeconds(5));
                return null;
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime),
                    ExecutionStarted = new ExecutionStartedEvent
                    {
                        Name = "wf",
                        Input = "123"
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5))
                    }
                },
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(2)),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                },
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "myevent"
                    }
                },
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5)),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                },
                new HistoryEvent
                {
                    TimerFired = new TimerFiredEvent
                    {
                        TimerId = 0,
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5))
                    }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!;
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);
        Assert.Equal(string.Empty, complete.Result);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnFailureDetails_WhenTimerFires()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();
        var beginDateTime = new DateTime(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc);

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: async (ctx, _) =>
            {
                await ctx.WaitForExternalEventAsync<object>("MyEvent", TimeSpan.FromSeconds(5));
                return null;
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime),
                    ExecutionStarted = new ExecutionStartedEvent
                    {
                        Name = "wf",
                        Input = "123"
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5))
                    }
                },
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5)),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                },
                new HistoryEvent
                {
                    TimerFired = new TimerFiredEvent
                    {
                        TimerId = 0,
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(5))
                    }
                },
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(beginDateTime.AddSeconds(10)),
                    OrchestratorStarted = new OrchestratorStartedEvent()
                },
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "myevent"
                    }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!;
        Assert.Equal(OrchestrationStatus.Failed, complete.OrchestrationStatus);
        Assert.NotNull(complete.FailureDetails);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldUseEmptyInstanceId_WhenOrchestrationInstanceIsNull_AndReturnEmptyResult_WhenOutputIsNull()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) => Task.FromResult<object?>(null))); // null output -> empty string result

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 9,
            OrchestrationInstance = null,
            Input = "" // empty input -> no deserialization branch
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Equal(string.Empty, response.InstanceId);
        Assert.Equal(9, response.TaskId);
        Assert.Null(response.FailureDetails);
        Assert.Equal(string.Empty, response.Result);
    }
    
    // -------------------------------------------------------------------------
    // RequiresHistoryStreaming
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldStreamHistory_WhenRequiresHistoryStreamingIsTrue()
    {
        // When RequiresHistoryStreaming is set, the worker must fetch past history
        // via StreamInstanceHistory and merge it with the inline PastEvents before
        // running the workflow. Here we put the ExecutionStarted event inside the
        // stream (not in PastEvents) so the workflow can only complete if streaming works.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: (_, input) => Task.FromResult<object?>((int)input! + 1)));

        // The streamed chunk carries the ExecutionStarted event.
        var streamedChunk = new HistoryChunk();
        streamedChunk.Events.Add(new HistoryEvent
        {
            ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "10" }
        });

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StreamInstanceHistory(It.IsAny<StreamInstanceHistoryRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateHistoryStreamingCall(SingleItemAsync(streamedChunk)));

        var worker = new WorkflowWorker(
            grpcClientMock.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "stream-i",
            RequiresHistoryStreaming = true
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("stream-i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteOrchestration != null).CompleteOrchestration!;
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);
        Assert.Equal("11", complete.Result);

        grpcClientMock.Verify(
            x => x.StreamInstanceHistory(It.IsAny<StreamInstanceHistoryRequest>(), It.IsAny<CallOptions>()),
            Times.Once());
    }

    // -------------------------------------------------------------------------
    // Workflow-name extraction fallbacks
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldExtractWorkflowName_FromHistoryState()
    {
        // When no ExecutionStarted event is present, the worker falls back to
        // HistoryState.OrchestrationState.Name to identify the workflow.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("fallback-wf", new InlineWorkflow(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(null)));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                // Only a HistoryState event, no ExecutionStarted.
                new HistoryEvent
                {
                    HistoryState = new HistoryStateEvent
                    {
                        OrchestrationState = new OrchestrationState { Name = "fallback-wf" }
                    }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldExtractWorkflowName_FromOrchestratorStartedVersion()
    {
        // Third fallback: OrchestratorStarted.Version.Name when both ExecutionStarted
        // and HistoryState are absent or have no name.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("version-wf", new InlineWorkflow(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(null)));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                        new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    OrchestratorStarted = new OrchestratorStartedEvent
                    {
                        Version = new OrchestrationVersion { Name = "version-wf" }
                    }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);
    }

    // -------------------------------------------------------------------------
    // IWorkflowRouterRegistry integration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldResolveFromRouter_WhenHistoryHasNoWorkflowName()
    {
        // When the history carries no workflow name, the worker consults
        // IWorkflowRouterRegistry.TryResolveLatest and stamps the version in the response.
        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("routed-wf", new InlineWorkflow(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(null)));

        var routerRegistry = new Mock<IWorkflowRouterRegistry>();
        routerRegistry.Setup(r => r.TryResolveLatest(out It.Ref<string>.IsAny))
            .Returns((out string name) =>
            {
                name = "routed-wf";
                return true;
            });
        routerRegistry.Setup(r => r.Contains(It.IsAny<string>())).Returns(true);

        var sp = new ServiceCollection()
            .AddSingleton(routerRegistry.Object)
            .BuildServiceProvider();

        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // No ExecutionStarted / HistoryState / OrchestratorStarted.Version — name must come from router.
        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent { TimerFired = new TimerFiredEvent() }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Completed, complete.OrchestrationStatus);

        // resolvedFromRouter=true must stamp the version into the response.
        Assert.NotNull(response.Version);
        Assert.Equal("routed-wf", response.Version.Name);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnWorkflowNameMissingError_WhenRouterCannotResolveLatest()
    {
        // If IWorkflowRouterRegistry.TryResolveLatest returns false and no name is
        // in the history, the worker must fail the orchestration with WorkflowNameMissing.
        var routerRegistry = new Mock<IWorkflowRouterRegistry>();
        routerRegistry.Setup(r => r.TryResolveLatest(out It.Ref<string>.IsAny))
            .Returns((out string name) =>
            {
                name = null!;
                return false;
            });

        var sp = new ServiceCollection()
            .AddSingleton(routerRegistry.Object)
            .BuildServiceProvider();

        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents = { new HistoryEvent { TimerFired = new TimerFiredEvent() } }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Failed, complete.OrchestrationStatus);
        Assert.Equal("WorkflowNameMissing", complete.FailureDetails.ErrorType);
    }

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnWorkflowNotFoundError_WhenRouterDoesNotContainWorkflow()
    {
        // If IWorkflowRouterRegistry is present but Contains("wf") returns false,
        // the worker must fail the orchestration with WorkflowNotFound.
        var routerRegistry = new Mock<IWorkflowRouterRegistry>();
        routerRegistry.Setup(r => r.Contains("wf")).Returns(false);

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(null)));

        var sp = new ServiceCollection()
            .AddSingleton(routerRegistry.Object)
            .BuildServiceProvider();

        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Failed, complete.OrchestrationStatus);
        Assert.Equal("WorkflowNotFound", complete.FailureDetails.ErrorType);
        Assert.Contains("wf", complete.FailureDetails.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Version stamping via IsPatched
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldIncludeVersionInResponse_WhenWorkflowUsesIsPatched()
    {
        // When the workflow calls ctx.IsPatched(), the version tracker sets
        // IncludeVersionInNextResponse=true and the worker stamps the version into
        // the OrchestratorResponse.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        const string patchName = "my-feature-patch";

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: (ctx, _) =>
            {
                ctx.IsPatched(patchName);
                return Task.FromResult<object?>(null);
            }));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.NotNull(response.Version);
        Assert.Equal("wf", response.Version.Name);
        Assert.Contains(patchName, response.Version.Patches);
    }

    // -------------------------------------------------------------------------
    // Yield path (workflow not yet complete)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldYield_WhenWorkflowAwaitsActivityNotYetComplete()
    {
        // First turn: workflow awaits an activity but no completion event is present.
        // The response must contain a ScheduleTask action and no CompleteOrchestration.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.CallActivityAsync<int>("step");
                return null;
            }));
        factory.AddActivity("step", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(42)));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // Only ExecutionStarted — activity has not been scheduled or completed yet.
        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        Assert.DoesNotContain(response.Actions, a => a.CompleteOrchestration != null);
        Assert.Contains(response.Actions, a => a.ScheduleTask != null);
    }

    // -------------------------------------------------------------------------
    // Inner exception path (workflow returns a faulted Task)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnFailedCompletion_WhenWorkflowReturnsFaultedTask()
    {
        // Unlike a synchronous throw (which hits the outer catch), a workflow that
        // returns an already-faulted Task exercises the inner try/catch around
        // `await runTask`, where IsNonRetriable is explicitly set to true.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(int),
            run: (_, _) => Task.FromException<object?>(new InvalidOperationException("task-fault"))));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "1" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Failed, complete.OrchestrationStatus);
        Assert.NotNull(complete.FailureDetails);
        Assert.Contains("task-fault", complete.FailureDetails.ErrorMessage);
        // The inner catch always marks workflow failures as non-retriable.
        Assert.True(complete.FailureDetails.IsNonRetriable);
    }

    // -------------------------------------------------------------------------
    // Outer exception handler (unexpected exception escaping the main logic)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldReturnFailed_WhenUnexpectedExceptionEscapes()
    {
        // An exception thrown by the serializer during input deserialization is
        // outside the inner try/catch, so it is caught by the outer handler which
        // returns an OrchestratorResponse with OrchestrationStatus.Failed.
        var sp = new ServiceCollection().BuildServiceProvider();
        var options = new WorkflowRuntimeOptions();

        var faultySerializer = new Mock<IWorkflowSerializer>();
        faultySerializer
            .Setup(s => s.Deserialize(It.IsAny<string>(), It.IsAny<Type>()))
            .Throws(new InvalidOperationException("serializer-exploded"));

        var factory = new StubWorkflowsFactory();
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(null)));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            faultySerializer.Object,
            sp,
            options);

        var request = new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                // Non-empty input triggers Deserialize.
                new HistoryEvent
                {
                    ExecutionStarted = new ExecutionStartedEvent { Name = "wf", Input = "\"some-input\"" }
                }
            }
        };

        var response = await InvokeHandleOrchestratorResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.NotNull(complete);
        Assert.Equal(OrchestrationStatus.Failed, complete.OrchestrationStatus);
        Assert.NotNull(complete.FailureDetails);
        Assert.Contains("serializer-exploded", complete.FailureDetails.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Activity: TaskExecutionId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldUseTaskExecutionId_WhenProvided()
    {
        // When TaskExecutionId is non-empty the activity context execution key must
        // be the TaskExecutionId string.  We verify this indirectly by confirming
        // the activity still executes successfully and the correct task id is echoed.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, _) => Task.FromResult<object?>(null)));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        const string execId = "exec-abc-123";

        var request = new ActivityRequest
        {
            Name = "act",
            TaskId = 77,
            TaskExecutionId = execId,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "wf-1" },
            Input = ""
        };

        var response = await InvokeHandleActivityResponseAsync(worker, request);

        Assert.Null(response.FailureDetails);
        Assert.Equal("wf-1", response.InstanceId);
        Assert.Equal(77, response.TaskId);
    }

    // -------------------------------------------------------------------------
    // CompletionToken propagation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleOrchestratorResponseAsync_ShouldEchoCompletionToken_InAllResponsePaths()
    {
        // CompletionToken must be round-tripped back in every response path.
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();
        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            new StubWorkflowsFactory(),
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // Suspended path
        var suspendedResponse = await InvokeHandleOrchestratorResponseAsync(worker, new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents = { new HistoryEvent { ExecutionStarted = new ExecutionStartedEvent { Name = "wf" } } },
            NewEvents = { new HistoryEvent { ExecutionSuspended = new ExecutionSuspendedEvent() } }
        });
        Assert.Equal(CompletionTokenValue, suspendedResponse.CompletionToken);

        // Terminated path
        var terminatedResponse = await InvokeHandleOrchestratorResponseAsync(worker, new OrchestratorRequest
        {
            InstanceId = "i",
            NewEvents = { new HistoryEvent { ExecutionTerminated = new ExecutionTerminatedEvent() } }
        });
        Assert.Equal(CompletionTokenValue, terminatedResponse.CompletionToken);

        // WorkflowNotFound path
        var notFoundResponse = await InvokeHandleOrchestratorResponseAsync(worker, new OrchestratorRequest
        {
            InstanceId = "i",
            PastEvents = { new HistoryEvent { ExecutionStarted = new ExecutionStartedEvent { Name = "not-registered" } } }
        });
        Assert.Equal(CompletionTokenValue, notFoundResponse.CompletionToken);
    }

    [Fact]
    public async Task HandleActivityResponseAsync_ShouldEchoCompletionToken_InAllResponsePaths()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("ok-act", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>(null)));

        var worker = new WorkflowWorker(
            CreateGrpcClientMock().Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp,
            options);

        // Success path
        var successResponse = await InvokeHandleActivityResponseAsync(worker, new ActivityRequest
        {
            Name = "ok-act",
            TaskId = 1,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" },
            Input = ""
        });
        Assert.Equal(CompletionTokenValue, successResponse.CompletionToken);

        // Not-found path
        var notFoundResponse = await InvokeHandleActivityResponseAsync(worker, new ActivityRequest
        {
            Name = "missing-act",
            TaskId = 2,
            OrchestrationInstance = new OrchestrationInstance { InstanceId = "i" },
            Input = ""
        });
        Assert.Equal(CompletionTokenValue, notFoundResponse.CompletionToken);
    }

    private const string CompletionTokenValue = "abc123";

    private static async Task InvokeExecuteAsync(WorkflowWorker worker, CancellationToken token)
    {
        var method = typeof(WorkflowWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method.Invoke(worker, [token])!;
        await task;
    }

    private static CallOptions InvokeCreateCallOptions(WorkflowWorker worker, CancellationToken token)
    {
        var method = typeof(WorkflowWorker).GetMethod("CreateCallOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (CallOptions)method.Invoke(worker, [token])!;
    }

    private static bool HasHeader(CallOptions options, string key, out string? value)
    {
        var entry = options.Headers?.FirstOrDefault(header => string.Equals(header.Key, key, StringComparison.OrdinalIgnoreCase));
        value = entry?.Value;
        return entry is not null;
    }

    private static async Task<OrchestratorResponse> InvokeHandleOrchestratorResponseAsync(WorkflowWorker worker, OrchestratorRequest request)
    {
        var method = typeof(WorkflowWorker).GetMethod("HandleOrchestratorResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<OrchestratorResponse>)method.Invoke(worker, [request, CompletionTokenValue])!;
        return await task;
    }

    private static async Task<ActivityResponse> InvokeHandleActivityResponseAsync(WorkflowWorker worker, ActivityRequest request)
    {
        var method = typeof(WorkflowWorker).GetMethod("HandleActivityResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<ActivityResponse>)method.Invoke(worker, [request, CompletionTokenValue])!;
        return await task;
    }

    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> CreateGrpcClientMock()
    {
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        return new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(callInvoker.Object);
    }

    private static AsyncServerStreamingCall<WorkItem> CreateServerStreamingCall(IAsyncEnumerable<WorkItem> items)
    {
        var stream = new TestAsyncStreamReader(items);

        return new AsyncServerStreamingCall<WorkItem>(
            stream,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

    private static AsyncServerStreamingCall<HistoryChunk> CreateHistoryStreamingCall(IAsyncEnumerable<HistoryChunk> chunks)
    {
        var stream = new TestAsyncStreamReader<HistoryChunk>(chunks);

        return new AsyncServerStreamingCall<HistoryChunk>(
            stream,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

    private static async IAsyncEnumerable<T> SingleItemAsync<T>(T item)
    {
        await Task.CompletedTask;
        yield return item;
    }

    private sealed class TestAsyncStreamReader(IAsyncEnumerable<WorkItem> items) : IAsyncStreamReader<WorkItem>
    {
        private readonly IAsyncEnumerator<WorkItem> _enumerator = items.GetAsyncEnumerator();
        public WorkItem Current => _enumerator.Current;
        public Task<bool> MoveNext(CancellationToken cancellationToken) => _enumerator.MoveNextAsync().AsTask();
    }

    private sealed class TestAsyncStreamReader<T>(IAsyncEnumerable<T> items) : IAsyncStreamReader<T>
    {
        private readonly IAsyncEnumerator<T> _enumerator = items.GetAsyncEnumerator();
        public T Current => _enumerator.Current;
        public Task<bool> MoveNext(CancellationToken cancellationToken) => _enumerator.MoveNextAsync().AsTask();
    }

    private sealed class StubWorkflowsFactory : IWorkflowsFactory
    {
        private readonly Dictionary<string, IWorkflow> _workflows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IWorkflowActivity> _activities = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> _workflowActivationErrors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> _activityActivationErrors = new(StringComparer.OrdinalIgnoreCase);

        public void AddWorkflow(string name, IWorkflow wf) => _workflows[name] = wf;
        public void AddActivity(string name, IWorkflowActivity act) => _activities[name] = act;
        public void AddWorkflowActivationError(string name, Exception ex) => _workflowActivationErrors[name] = ex;
        public void AddActivityActivationError(string name, Exception ex) => _activityActivationErrors[name] = ex;

        public void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow => throw new NotSupportedException();
        public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput, Task<TOutput>> implementation) => throw new NotSupportedException();
        public void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity => throw new NotSupportedException();
        public void RegisterActivity<TInput, TOutput>(string name, Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation) => throw new NotSupportedException();

        public bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflow? workflow,
            out Exception? activationException)
        {
            if (_workflowActivationErrors.TryGetValue(identifier.Name, out var ex))
            {
                activationException = ex;
                workflow = null;
                return false;
            }
            activationException = null;
            return _workflows.TryGetValue(identifier.Name, out workflow);
        }

        public bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflowActivity? activity,
            out Exception? activationException)
        {
            if (_activityActivationErrors.TryGetValue(identifier.Name, out var ex))
            {
                activationException = ex;
                activity = null;
                return false;
            }
            activationException = null;
            return _activities.TryGetValue(identifier.Name, out activity);
        }
    }

    private sealed class InlineWorkflow(Type inputType, Func<WorkflowContext, object?, Task<object?>> run) : IWorkflow
    {
        public Type InputType { get; } = inputType;
        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(WorkflowContext context, object? input) => run(context, input);
    }

    private sealed class InlineActivity(Type inputType, Func<WorkflowActivityContext, object?, Task<object?>> run) : IWorkflowActivity
    {
        public Type InputType { get; } = inputType;
        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(WorkflowActivityContext context, object? input) => run(context, input);
    }

    private static async IAsyncEnumerable<WorkItem> EmptyWorkItems()
    {
        await Task.CompletedTask;
        yield break;
    }
}
