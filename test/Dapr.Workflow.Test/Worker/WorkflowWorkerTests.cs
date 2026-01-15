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

using System.Reflection;
using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Worker;
using Dapr.Workflow.Worker.Grpc;
using Dapr.Workflow.Worker.Internal;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Type = System.Type;

namespace Dapr.Workflow.Test.Worker;

public class WorkflowWorkerTests
{
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

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateServerStreamingCall(EmptyWorkItems()));

        var worker = new WorkflowWorker(
            grpcClientMock.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            services,
            options);

        await InvokeExecuteAsync(worker, CancellationToken.None);
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
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns((GetWorkItemsRequest _, Metadata? _, DateTime? _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
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
    public async Task CallChildWorkflowAsync_ShouldComplete_WhenCompletionEventArrivesLater()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance, null);

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
            serializer, NullLoggerFactory.Instance, appId1);

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
            serializer, NullLoggerFactory.Instance, null);

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
            serializer, NullLoggerFactory.Instance, null);

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
            serializer, NullLoggerFactory.Instance, null);

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
            serializer, NullLoggerFactory.Instance, null);

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
        Assert.Empty(response.Actions);
        Assert.Null(response.CustomStatus);
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
        Assert.Empty(response.Actions);
        Assert.Null(response.CustomStatus);
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
    public async Task ExecuteAsync_ShouldRethrow_WhenGrpcProtocolHandlerStartFailsWithException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkItems(It.IsAny<GetWorkItemsRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"));

        var worker = new WorkflowWorker(
            grpcClientMock.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            services,
            options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeExecuteAsync(worker, CancellationToken.None));
        Assert.Contains("boom", ex.Message);
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
    public async Task HandleActivityResponseAsync_ShouldUseEmptyInstanceId_WhenOrchestrationInstanceIsNull_AndReturnEmptyResult_WhenOutputIsNull()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = new WorkflowRuntimeOptions();

        var factory = new StubWorkflowsFactory();
        factory.AddActivity("act", new InlineActivity(
            inputType: typeof(int),
            run: (_, __) => Task.FromResult<object?>(null))); // null output -> empty string result

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
    
    private static async Task InvokeExecuteAsync(WorkflowWorker worker, CancellationToken token)
    {
        var method = typeof(WorkflowWorker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method!.Invoke(worker, [token])!;
        await task;
    }

    private static async Task<OrchestratorResponse> InvokeHandleOrchestratorResponseAsync(WorkflowWorker worker, OrchestratorRequest request)
    {
        var method = typeof(WorkflowWorker).GetMethod("HandleOrchestratorResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<OrchestratorResponse>)method!.Invoke(worker, [request])!;
        return await task;
    }

    private static async Task<ActivityResponse> InvokeHandleActivityResponseAsync(WorkflowWorker worker, ActivityRequest request)
    {
        var method = typeof(WorkflowWorker).GetMethod("HandleActivityResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<ActivityResponse>)method!.Invoke(worker, [request])!;
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

    private sealed class TestAsyncStreamReader(IAsyncEnumerable<WorkItem> items) : IAsyncStreamReader<WorkItem>
    {
        private readonly IAsyncEnumerator<WorkItem> _enumerator = items.GetAsyncEnumerator();
        public WorkItem Current => _enumerator.Current;
        public Task<bool> MoveNext(CancellationToken cancellationToken) => _enumerator.MoveNextAsync().AsTask();
    }

    private sealed class StubWorkflowsFactory : IWorkflowsFactory
    {
        private readonly Dictionary<string, IWorkflow> _workflows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IWorkflowActivity> _activities = new(StringComparer.OrdinalIgnoreCase);

        public void AddWorkflow(string name, IWorkflow wf) => _workflows[name] = wf;
        public void AddActivity(string name, IWorkflowActivity act) => _activities[name] = act;

        public void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow => throw new NotSupportedException();
        public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput, Task<TOutput>> implementation) => throw new NotSupportedException();
        public void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity => throw new NotSupportedException();
        public void RegisterActivity<TInput, TOutput>(string name, Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation) => throw new NotSupportedException();

        public bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflow? workflow)
            => _workflows.TryGetValue(identifier.Name, out workflow);

        public bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflowActivity? activity)
            => _activities.TryGetValue(identifier.Name, out activity);
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
