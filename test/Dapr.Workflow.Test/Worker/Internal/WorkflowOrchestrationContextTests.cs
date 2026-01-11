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

using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Worker.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using JsonException = System.Text.Json.JsonException;

namespace Dapr.Workflow.Test.Worker.Internal;

public class WorkflowOrchestrationContextTests
{
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldComplete_WhenCompletionCorrelationIdMatchesParentTaskId()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var childTask = context.CallChildWorkflowAsync<int>("ChildWf");

        var history = new[]
        {
            new HistoryEvent
            {
                EventId = 0,
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            },
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 0,
                    Result = "42"
                }
            }
        };

        context.ProcessEvents(history, isReplaying: true);

        Assert.Equal(42, await childTask);
        Assert.Empty(context.PendingActions);
    }
    
    [Fact]
    public async Task CallChildWorkflowAsync_ShouldComplete_WhenRuntimeUsesCreatedEventIdAsCompletionCorrelationId()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        // Force a known child instance id so we can correlate created.InstanceId -> parent task id
        const string childInstanceId = "child-xyz";

        var childTask = context.CallChildWorkflowAsync<int>(
            workflowName: "ChildWf",
            input: 7,
            options: new ChildWorkflowTaskOptions(InstanceId: childInstanceId));

        var history = new[]
        {
            new HistoryEvent
            {
                EventId = 100,
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent
                {
                    Name = "ChildWf",
                    InstanceId = childInstanceId
                }
            },
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent
                {
                    TaskScheduledId = 100,
                    Result = "21"
                }
            }
        };

        context.ProcessEvents(history, isReplaying: true);

        var result = await childTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(21, result);
        Assert.Empty(context.PendingActions);
    }
    
    [Fact]
    public void CallChildWorkflowAsync_ShouldPutRouterOnCreateSubOrchestrationAction_WhenAppIdProvided()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        _ = context.CallChildWorkflowAsync<int>(
            workflowName: "ChildWf",
            input: 1,
            options: new ChildWorkflowTaskOptions(InstanceId: "child-1", AppId: "remote-app"));

        var action = Assert.Single(context.PendingActions);
        Assert.NotNull(action.CreateSubOrchestration);

        Assert.NotNull(action.CreateSubOrchestration.Router);
        Assert.Equal("remote-app", action.CreateSubOrchestration.Router.TargetAppID);

        // wrapper router may exist too (compat), but inner is the important one
        Assert.NotNull(action.Router);
        Assert.Equal("remote-app", action.Router.TargetAppID);
    }
    
    [Fact]
    public void CallActivityAsync_ShouldNotSetRouter_WhenAppIdNotProvided()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        _ = context.CallActivityAsync<int>(name: "MyActivity", input: 2);

        var action = Assert.Single(context.PendingActions);
        Assert.NotNull(action.ScheduleTask);

        Assert.Null(action.ScheduleTask.Router);
        Assert.Null(action.Router);
    }

    [Fact]
    public void CallActivityAsync_ShouldPutRouterOnScheduleTaskAction_WhenAppIdProvided()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        _ = context.CallActivityAsync<int>(
            name: "MyActivity",
            input: 2,
            options: new WorkflowTaskOptions(AppId: "remote-app"));

        var action = Assert.Single(context.PendingActions);
        Assert.NotNull(action.ScheduleTask);

        Assert.NotNull(action.ScheduleTask.Router);
        Assert.Equal("remote-app", action.ScheduleTask.Router.TargetAppID);

        Assert.NotNull(action.Router);
        Assert.Equal("remote-app", action.Router.TargetAppID);
    }
    
    [Fact]
    public void CallActivityAsync_ShouldScheduleTaskAction_WhenNotReplaying()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallActivityAsync<string>("DoWork", input: new { Value = 3 });

        Assert.NotNull(task);
        Assert.Single(context.PendingActions);

        var action = context.PendingActions.First();
        Assert.NotNull(action.ScheduleTask);
        Assert.Equal("DoWork", action.ScheduleTask.Name);
        Assert.Contains("\"value\":3", action.ScheduleTask.Input);
    }

    [Fact]
    public async Task CallActivityAsync_ShouldReturnCompletedResult_FromHistoryTaskCompleted()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TaskScheduled = new TaskScheduledEvent
                {
                    Name = "Any",
                }
            },
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { Result = "\"hello\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallActivityAsync<string>("Any");
        context.ProcessEvents(history, true);

        var result = await task;
        Assert.Equal("hello", result);
        Assert.Empty(context.PendingActions);
    }

    [Fact]
    public async Task CallActivityAsync_ShouldReturnCompletedResult_FromCallFooTaskCompletedFirst()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventId = 0,
                TaskScheduled = new TaskScheduledEvent
                {
                    Name = "CallFoo",
                }
            },
            new HistoryEvent
            {
                EventId = 1,
                TaskScheduled = new TaskScheduledEvent
                {
                    Name = "CallBar",
                }
            },
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { TaskScheduledId = 0, Result = "\"foo\"" }
            },
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { TaskScheduledId = 1, Result = "\"bar\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var fooTask = context.CallActivityAsync<string>("CallFoo");
        var barTask = context.CallActivityAsync<string>("CallBar");
        var task = Task.WhenAny(fooTask, barTask);
        context.ProcessEvents(history, true);

        Task winner = await task;
        Assert.Equal(fooTask, winner);

        var value = await fooTask;
        Assert.Equal("foo", value);
        Assert.True(barTask.IsCompleted);
        Assert.Empty(context.PendingActions);

        value = await barTask;
        Assert.Equal("bar", value);
    }

    [Fact]
    public async Task CallActivityAsync_ShouldReturnCompletedResult_FromCallBarTaskCompletedFirst()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventId = 0,
                TaskScheduled = new TaskScheduledEvent
                {
                    Name = "CallFoo",
                }
            },
            new HistoryEvent
            {
                EventId = 1,
                TaskScheduled = new TaskScheduledEvent
                {
                    Name = "CallBar",
                }
            },
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { TaskScheduledId = 1, Result = "\"bar\"" }
            },
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { TaskScheduledId = 0, Result = "\"foo\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var fooTask = context.CallActivityAsync<string>("CallFoo");
        var barTask = context.CallActivityAsync<string>("CallBar");
        var task = Task.WhenAny(fooTask, barTask);
        context.ProcessEvents(history, true);

        Task winner = await task;
        Assert.Equal(barTask, winner);

        var value = await barTask;
        Assert.Equal("bar", value);
        Assert.True(fooTask.IsCompleted);
        Assert.Empty(context.PendingActions);

        value = await fooTask;
        Assert.Equal("foo", value);
    }

    [Fact]
    public async Task CallActivityAsync_ShouldThrowWorkflowTaskFailedException_FromHistoryTaskFailed()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TaskScheduled = new TaskScheduledEvent
                {
                    Name = "Any",
                }
            },
            new HistoryEvent
            {
                TaskFailed = new TaskFailedEvent
                {
                    FailureDetails = new TaskFailureDetails
                    {
                        ErrorType = "MyError",
                        ErrorMessage = "Boom",
                        StackTrace = "trace"
                    }
                }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallActivityAsync<string>("Any");
        context.ProcessEvents(history, true);

        await Assert.ThrowsAsync<WorkflowTaskFailedException>(() => task);
        Assert.Empty(context.PendingActions);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_ShouldReturnDeserializedValue_WhenEventInHistory_IgnoringCase()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent { Name = "MyEvent", Input = "123" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.WaitForExternalEventAsync<int>("myevent");
        context.ProcessEvents(history, true);

        var value = await task;
        Assert.Equal(123, value);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_ShouldReturnUncompletedTask_WhenEventInHistory()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent { Name = "MyEvent", Input = "123" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        var task = context.WaitForExternalEventAsync<int>("myevent", cts.Token);
        context.ProcessEvents(history, true);

        Assert.True(task.IsCompleted);

        var value = await task;
        Assert.Equal(123, value);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_ShouldReturnUncompletedTask_WhenEventNotInHistory()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        var task = context.WaitForExternalEventAsync<int>("missing-event", cts.Token);

        Assert.False(task.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_WithTimeoutOverload_ShouldCancel_WhenEventReceived()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent { Name = "MyEvent", Input = "123" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.WaitForExternalEventAsync<int>("myevent", TimeSpan.FromMilliseconds(25));
        context.ProcessEvents(history, true);

        Assert.True(task.IsCompleted);

        var value = await task;
        Assert.Equal(123, value);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_WithTimeoutOverload_ShouldCancel_WhenEventNotReceived()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TimerFired = new TimerFiredEvent()
            },
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent { Name = "MyEvent", Input = "123" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.WaitForExternalEventAsync<int>("missing-event", TimeSpan.FromMilliseconds(25));
        context.ProcessEvents(history, true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void SetCustomStatus_ShouldUpdateCustomStatusProperty()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        Assert.Null(context.CustomStatus);

        var status = new { Step = 2, Message = "working" };
        context.SetCustomStatus(status);

        Assert.Same(status, context.CustomStatus);

        context.SetCustomStatus(null);

        Assert.Null(context.CustomStatus);
    }

    [Fact]
    public void IsReplaying_ShouldBeTrue_WhenHistoryHasUnconsumedEvents_AndFalseAfterConsumption()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { Result = "\"ok\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);


        _ = context.CallActivityAsync<string>("Any");
        context.ProcessEvents(history, true);
        Assert.True(context.IsReplaying);

        context.ProcessEvents([], false);
        Assert.False(context.IsReplaying);
    }

    [Fact]
    public async Task CreateTimer_ShouldReturnCompletedTask_WhenTimerFiredInHistory()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TimerCreated = new TimerCreatedEvent()
            },
            new HistoryEvent
            {
                TimerFired = new TimerFiredEvent()
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CreateTimer(DateTime.UtcNow.AddMinutes(1), CancellationToken.None);
        context.ProcessEvents(history, true);

        await task;

        Assert.Empty(context.PendingActions);
    }

    // [Fact]
    // public async Task CreateTimer_ShouldScheduleAction_AndRemoveItOnCancellation()
    // {
    //     var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    //
    //     var context = new WorkflowOrchestrationContext(
    //         name: "wf",
    //         instanceId: "i",
    //         pastEvents: [],
    //         newEvents: [],
    //         currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
    //         workflowSerializer: serializer,
    //         loggerFactory: NullLoggerFactory.Instance);
    //
    //     using var cts = new CancellationTokenSource();
    //
    //     var timerTask = context.CreateTimer(DateTime.UtcNow.AddSeconds(3), cts.Token);
    //
    //     Assert.Single(context.PendingActions);
    //     Assert.NotNull(context.PendingActions[0].CreateTimer);
    //     Assert.False(timerTask.IsCompleted);
    //
    //     await cts.CancelAsync();
    //
    //     await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timerTask);
    //
    //     Assert.Empty(context.PendingActions);
    // }

    [Fact]
    public void SendEvent_ShouldAddSendEventAction_WithSerializedPayload()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        context.SendEvent("child-1", "evt", new { A = 1 });

        Assert.Single(context.PendingActions);
        var action = context.PendingActions.First();

        Assert.NotNull(action.SendEvent);
        Assert.Equal("evt", action.SendEvent.Name);
        Assert.Equal("child-1", action.SendEvent.Instance.InstanceId);
        Assert.Contains("\"a\":1", action.SendEvent.Data);
    }

    [Fact]
    public void CreateReplaySafeLogger_ShouldReturnLoggerThatIsDisabledDuringReplay()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { Result = "\"ok\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: new AlwaysEnabledLoggerFactory());

        var logger = context.CreateReplaySafeLogger("cat");

        _ = context.CallActivityAsync<string>("Any"); // consumes 1 history event
        context.ProcessEvents(history, true);
        Assert.True(context.IsReplaying);
        Assert.False(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information));

        context.ProcessEvents([], false);
        Assert.False(context.IsReplaying);
        Assert.True(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information));
    }

    [Fact]
    public void ContinueAsNew_ShouldAddCompleteOrchestrationAction_WithCarryoverEvents_WhenPreserveUnprocessedEventsIsTrue()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent { EventRaised = new EventRaisedEvent { Name = "e1", Input = "\"x\"" } },
            new HistoryEvent { EventRaised = new EventRaisedEvent { Name = "e2", Input = "\"y\"" } }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        context.ProcessEvents(history, true);
        context.ContinueAsNew(newInput: new { V = 9 }, preserveUnprocessedEvents: true);

        Assert.Single(context.PendingActions);
        var action = context.PendingActions.First();

        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.ContinuedAsNew, action.CompleteOrchestration.OrchestrationStatus);
        Assert.Contains("\"v\":9", action.CompleteOrchestration.Result);
        Assert.Equal(2, action.CompleteOrchestration.CarryoverEvents.Count);
    }

    [Fact]
    public void ContinueAsNew_ShouldNotCarryOverEvents_WhenPreserveUnprocessedEventsIsFalse()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent { EventRaised = new EventRaisedEvent { Name = "e1", Input = "\"x\"" } },
            new HistoryEvent { EventRaised = new EventRaisedEvent { Name = "e2", Input = "\"y\"" } }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        context.ProcessEvents(history, true);
        context.ContinueAsNew(newInput: null, preserveUnprocessedEvents: false);

        Assert.Single(context.PendingActions);
        var action = context.PendingActions.First();

        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.ContinuedAsNew, action.CompleteOrchestration.OrchestrationStatus);
        Assert.Empty(action.CompleteOrchestration.CarryoverEvents);
    }

    [Fact]
    public void NewGuid_ShouldBeDeterministic_ForSameInstanceIdAndTimestamp()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var now = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        var c1 = new WorkflowOrchestrationContext("wf", "00000000-0000-0000-0000-000000000001",
            now, serializer, NullLoggerFactory.Instance);

        var c2 = new WorkflowOrchestrationContext("wf", "00000000-0000-0000-0000-000000000001",
            now, serializer, NullLoggerFactory.Instance);

        var g1 = c1.NewGuid();
        var g2 = c2.NewGuid();

        Assert.Equal(g1, g2);
    }

    [Fact]
    public async Task CallActivityAsync_ShouldThrowArgumentException_WhenNameIsNullOrWhitespace()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => context.CallActivityAsync<int>(""));
        await Assert.ThrowsAsync<ArgumentException>(() => context.CallActivityAsync<int>("   "));
    }

    [Fact]
    public async Task CallActivityAsync_ShouldThrowInvalidOperationException_WhenHistoryEventIsUnexpectedType()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TimerFired = new TimerFiredEvent()
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallActivityAsync<int>("Act");
        context.ProcessEvents(history, true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("Unexpected history event type", ex.Message);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_ShouldReturnDefault_WhenEventInHistoryHasNullInput()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent { Name = "MyEvent", Input = null }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.WaitForExternalEventAsync<int>("MyEvent");
        context.ProcessEvents(history, true);
        var value = await task;

        Assert.Equal(default, value);
    }

    [Fact]
    public async Task WaitForExternalEventAsync_WithTimeoutOverload_ShouldReturnResult_WhenEventIsInHistory()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                EventRaised = new EventRaisedEvent { Name = "MyEvent", Input = "456" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.WaitForExternalEventAsync<int>("MyEvent", TimeSpan.FromSeconds(10));
        context.ProcessEvents(history, true);
        var value = await task;

        Assert.Equal(456, value);
    }

    [Fact]
    public void CallChildWorkflowAsync_ShouldScheduleSubOrchestration_WhenNotReplaying_AndUseProvidedInstanceId()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallChildWorkflowAsync<int>(
            workflowName: "ChildWf",
            input: new { V = 1 },
            options: new ChildWorkflowTaskOptions { InstanceId = "child-123" });

        Assert.False(task.IsCompleted);

        Assert.Single(context.PendingActions);
        var action = context.PendingActions.First();

        Assert.NotNull(action.CreateSubOrchestration);
        Assert.Equal("ChildWf", action.CreateSubOrchestration.Name);
        Assert.Equal("child-123", action.CreateSubOrchestration.InstanceId);
        Assert.Contains("\"v\":1", action.CreateSubOrchestration.Input);
    }

    [Fact]
    public async Task CallChildWorkflowAsync_ShouldReturnCompletedResult_FromHistorySubOrchestrationCompleted()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            },
            new HistoryEvent
            {
                SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent { Result = "42" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallChildWorkflowAsync<int>("ChildWf");
        context.ProcessEvents(history, true);
        var value = await task;

        Assert.Equal(42, value);
        Assert.Empty(context.PendingActions);
    }

    [Fact]
    public async Task CallChildWorkflowAsync_ShouldThrowWorkflowTaskFailedException_FromHistorySubOrchestrationFailed()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" }
            },
            new HistoryEvent
            {
                SubOrchestrationInstanceFailed = new SubOrchestrationInstanceFailedEvent
                {
                    FailureDetails = new TaskFailureDetails
                    {
                        ErrorType = "ChildError",
                        ErrorMessage = "boom",
                        StackTrace = "trace"
                    }
                }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallChildWorkflowAsync<int>("ChildWf");
        context.ProcessEvents(history, true);

        var ex = await Assert.ThrowsAsync<WorkflowTaskFailedException>(() => task);
        Assert.Contains("Child workflow 'ChildWf' failed", ex.Message);
        Assert.Equal("ChildError", ex.FailureDetails.ErrorType);
        Assert.Equal("boom", ex.FailureDetails.ErrorMessage);
        Assert.Equal("trace", ex.FailureDetails.StackTrace);
    }

    [Fact]
    public async Task CallChildWorkflowAsync_ShouldThrowJsonDeserializationException_WhenHistoryEventIsUnexpectedType()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { Result = "\"not-child\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "parent",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallChildWorkflowAsync<int>("ChildWf");
        context.ProcessEvents(history, true);

        var ex = await Assert.ThrowsAsync<JsonException>(() => task);
        Assert.Contains("The JSON value could not be converted to ", ex.Message);
    }

    [Fact]
    public void CreateReplaySafeLogger_TypeAndGenericOverloads_ShouldBehaveLikeCategoryOverload()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
            new HistoryEvent
            {
                TaskCompleted = new TaskCompletedEvent { Result = "\"ok\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: new AlwaysEnabledLoggerFactory());


        var typeLogger = context.CreateReplaySafeLogger(typeof(WorkflowOrchestrationContextTests));
        var genericLogger = context.CreateReplaySafeLogger<WorkflowOrchestrationContextTests>();

        _ = context.CallActivityAsync<string>("Any"); // consumes 1 history event
        context.ProcessEvents(history, true);
        Assert.True(context.IsReplaying);
        Assert.False(typeLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information));
        Assert.False(genericLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information));

        context.ProcessEvents([], false);
        Assert.False(context.IsReplaying);
        Assert.True(typeLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information));
        Assert.True(genericLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information));
    }

    private sealed class AlwaysEnabledLoggerFactory : Microsoft.Extensions.Logging.ILoggerFactory
    {
        public void AddProvider(Microsoft.Extensions.Logging.ILoggerProvider provider) { }
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new AlwaysEnabledLogger();
        public void Dispose() { }
    }

    private sealed class AlwaysEnabledLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
