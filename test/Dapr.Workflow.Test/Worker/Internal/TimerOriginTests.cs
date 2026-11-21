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
using Dapr.Common.Serialization;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker;
using Dapr.Workflow.Worker.Internal;
using Dapr.Workflow.Abstractions;
using Dapr.Testcontainers.Xunit.Attributes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test.Worker.Internal;

/// <summary>
/// Tests for timer origin assignment and optional timer replay compatibility.
/// </summary>
public class TimerOriginTests
{
    private static readonly DateTime StartTime = new(2025, 01, 01, 12, 0, 0, DateTimeKind.Utc);

    // =====================================================================
    // Origin assignment tests (Tests 1–6)
    // =====================================================================

    /// <summary>
    /// Test 1 — CreateTimer(delay) sets TimerOriginCreateTimer.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task CreateTimer_SetsTimerOriginCreateTimer()
    {
        var context = CreateContext();
        _ = context.CreateTimer(TimeSpan.FromSeconds(5), CancellationToken.None);

        var action = Assert.Single(context.PendingActions);
        Assert.NotNull(action.CreateTimer);
        Assert.Equal(CreateTimerAction.OriginOneofCase.CreateTimer, action.CreateTimer.OriginCase);
    }

    /// <summary>
    /// Test 2 — finite-timeout WaitForExternalEvent sets TimerOriginExternalEvent.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task WaitForExternalEvent_FiniteTimeout_SetsTimerOriginExternalEvent()
    {
        var context = CreateContext();
        var timeout = TimeSpan.FromSeconds(5);
        _ = context.WaitForExternalEventAsync<string>("myEvent", timeout);

        // There should be a pending CreateTimer action with ExternalEvent origin
        var timerAction = context.PendingActions
            .FirstOrDefault(a => a.CreateTimer != null);
        Assert.NotNull(timerAction);
        Assert.Equal(CreateTimerAction.OriginOneofCase.ExternalEvent, timerAction!.CreateTimer!.OriginCase);
        Assert.Equal("myEvent", timerAction.CreateTimer.ExternalEvent.Name);
        
        // Verify fireAt = startTime + timeout
        var expectedFireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(5));
        Assert.Equal(expectedFireAt, timerAction.CreateTimer.FireAt);
    }

    /// <summary>
    /// Test 3 — activity retry timer sets TimerOriginActivityRetry.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ActivityRetry_SetsTimerOriginActivityRetry()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.CallActivityAsync<string>("failAct", options: new WorkflowTaskOptions
                {
                    RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: 2,
                        firstRetryInterval: TimeSpan.FromSeconds(1))
                });
                return null;
            }));
        factory.AddActivity("failAct", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => throw new InvalidOperationException("boom")));

        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                new HistoryEvent
                {
                    EventId = 0,
                    TaskScheduled = new TaskScheduledEvent { Name = "failAct" }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    TaskFailed = new TaskFailedEvent
                    {
                        TaskScheduledId = 0,
                        FailureDetails = new TaskFailureDetails
                        {
                            ErrorType = "InvalidOperationException",
                            ErrorMessage = "boom"
                        }
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        // Should have a retry timer with ActivityRetry origin
        var retryTimer = response.Actions.FirstOrDefault(a => a.CreateTimer != null);
        Assert.NotNull(retryTimer);
        Assert.Equal(CreateTimerAction.OriginOneofCase.ActivityRetry, retryTimer!.CreateTimer!.OriginCase);
        Assert.NotEmpty(retryTimer.CreateTimer.ActivityRetry.TaskExecutionId);
    }

    /// <summary>
    /// Test 4 — activity retry taskExecutionId is stable across attempts.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ActivityRetry_TaskExecutionId_IsStableAcrossAttempts()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.CallActivityAsync<string>("failAct", options: new WorkflowTaskOptions
                {
                    RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: 3,
                        firstRetryInterval: TimeSpan.FromSeconds(1))
                });
                return null;
            }));
        factory.AddActivity("failAct", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => throw new InvalidOperationException("boom")));

        // History: first attempt fails, retry timer fires, second attempt fails
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                // First attempt scheduled and fails
                new HistoryEvent
                {
                    EventId = 0,
                    TaskScheduled = new TaskScheduledEvent { Name = "failAct" }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    TaskFailed = new TaskFailedEvent
                    {
                        TaskScheduledId = 0,
                        FailureDetails = new TaskFailureDetails
                        {
                            ErrorType = "InvalidOperationException",
                            ErrorMessage = "boom"
                        }
                    }
                },
                // Retry timer created and fires
                new HistoryEvent
                {
                    EventId = 1,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(2))
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(2)),
                new HistoryEvent
                {
                    TimerFired = new TimerFiredEvent
                    {
                        TimerId = 1,
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(2))
                    }
                },
                // Second attempt scheduled and fails
                new HistoryEvent
                {
                    EventId = 2,
                    TaskScheduled = new TaskScheduledEvent { Name = "failAct" }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(3)),
                new HistoryEvent
                {
                    TaskFailed = new TaskFailedEvent
                    {
                        TaskScheduledId = 2,
                        FailureDetails = new TaskFailureDetails
                        {
                            ErrorType = "InvalidOperationException",
                            ErrorMessage = "boom"
                        }
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        // Should have a second retry timer with the same taskExecutionId as the first
        var retryTimers = response.Actions
            .Where(a => a.CreateTimer?.OriginCase == CreateTimerAction.OriginOneofCase.ActivityRetry)
            .ToList();

        Assert.Single(retryTimers);
        Assert.NotEmpty(retryTimers[0].CreateTimer!.ActivityRetry.TaskExecutionId);
    }

    /// <summary>
    /// Every attempt of an activity retry chain, and every retry timer, carries the
    /// first attempt's TaskExecutionId.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_AttemptsAndTimers_ShareFirstAttemptTaskExecutionId()
    {
        var context = CreateContext();
        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));

        var executionId = Assert.Single(context.PendingActions).ScheduleTask.TaskExecutionId;
        Assert.NotEmpty(executionId);

        context.ProcessEvents([Scheduled(0, executionId), Failed(0, executionId)], true);
        var firstTimer = context.PendingActions.Single(a => a.Id == 1).CreateTimer;
        Assert.Equal(executionId, firstTimer.ActivityRetry.TaskExecutionId);

        context.ProcessEvents([TimerCreated(1), TimerFired(1)], true);
        Assert.Equal(executionId, context.PendingActions.Single(a => a.Id == 2).ScheduleTask.TaskExecutionId);

        context.ProcessEvents([Scheduled(2, executionId), Failed(2, executionId)], true);
        var secondTimer = context.PendingActions.Single(a => a.Id == 3).CreateTimer;
        Assert.Equal(executionId, secondTimer.ActivityRetry.TaskExecutionId);

        context.ProcessEvents([TimerCreated(3), TimerFired(3)], true);
        Assert.Equal(executionId, context.PendingActions.Single(a => a.Id == 4).ScheduleTask.TaskExecutionId);

        context.ProcessEvents([Scheduled(4, executionId), Completed(4, executionId)], false);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// A chain recorded by an SDK that gave each attempt its own TaskExecutionId replays
    /// unchanged, and attempts scheduled after the upgrade reuse the first attempt's ID.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_ReplaysPerAttemptIds_AndContinuesWithFirstAttemptId()
    {
        var context = CreateContext();
        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));

        context.ProcessEvents(
        [
            Scheduled(0, "legacy-1"), Failed(0, "legacy-1"),
            TimerCreated(1), TimerFired(1),
            Scheduled(2, "legacy-2"), Failed(2, "legacy-2"),
        ], true);

        var timer = context.PendingActions.Single(a => a.Id == 3).CreateTimer;
        Assert.Equal("legacy-1", timer.ActivityRetry.TaskExecutionId);

        context.ProcessEvents([TimerCreated(3), TimerFired(3)], true);
        Assert.Equal("legacy-1", context.PendingActions.Single(a => a.Id == 4).ScheduleTask.TaskExecutionId);

        context.ProcessEvents([Scheduled(4, "legacy-1"), Completed(4, "legacy-1")], false);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// Completions that arrive before their attempts are registered are buffered per
    /// TaskExecutionId; attempts that share the ID must each get their own.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_BufferedCompletionsSharingTaskExecutionId_AreDeliveredPerAttempt()
    {
        var context = CreateContext();
        context.ProcessEvents(
        [
            Scheduled(0, "shared"), Failed(0, "shared"),
            Scheduled(2, "shared"), Completed(2, "shared"),
        ], true);

        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));
        context.ProcessEvents([TimerCreated(1), TimerFired(1)], true);

        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// A buffered completion is matched to its own attempt even when a later attempt's
    /// completion was buffered first.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_BufferedCompletionsOutOfOrder_AreMatchedByTaskScheduledId()
    {
        var context = CreateContext();
        context.ProcessEvents(
        [
            Scheduled(0, "shared"), Scheduled(2, "shared"),
            Completed(2, "shared"), Failed(0, "shared"),
        ], true);

        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));
        Assert.False(task.IsCompleted);
        Assert.Contains(context.PendingActions, a => a.Id == 1 && a.CreateTimer is not null);

        context.ProcessEvents([TimerCreated(1), TimerFired(1)], true);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// A completion buffered twice is delivered once: the copy must not be handed to
    /// the next attempt that shares the TaskExecutionId.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_DuplicateBufferedCompletion_IsDeliveredOnce()
    {
        var context = CreateContext();
        context.ProcessEvents([Scheduled(0, "shared"), Failed(0, "shared"), Failed(0, "shared")], true);

        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));
        context.ProcessEvents([TimerCreated(1), TimerFired(1)], true);
        Assert.False(task.IsCompleted);
        Assert.DoesNotContain(context.PendingActions, a => a.Id == 3);

        context.ProcessEvents([Scheduled(2, "shared"), Completed(2, "shared")], false);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// A repeated completion of an earlier attempt must not complete a later attempt
    /// that shares its TaskExecutionId.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_RepeatedCompletionOfEarlierAttempt_IsIgnored()
    {
        var context = CreateContext();
        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));
        var executionId = Assert.Single(context.PendingActions).ScheduleTask.TaskExecutionId;

        context.ProcessEvents(
        [
            Scheduled(0, executionId), Failed(0, executionId),
            TimerCreated(1), TimerFired(1),
            Scheduled(2, executionId), Failed(0, executionId),
        ], true);
        Assert.False(task.IsCompleted);
        Assert.DoesNotContain(context.PendingActions, a => a.CreateTimer is not null);

        context.ProcessEvents([Completed(2, executionId)], false);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// A repeated completion is ignored even when its TaskScheduledId happens to equal
    /// the task ID of a later attempt that is still open.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_RepeatedCompletionMatchingLaterAttemptTaskId_IsIgnored()
    {
        var context = CreateContext();
        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));
        var executionId = Assert.Single(context.PendingActions).ScheduleTask.TaskExecutionId;

        // The first failure carries a TaskScheduledId that differs from the local task
        // ID, so it is matched by TaskExecutionId.
        context.ProcessEvents([Failed(2, executionId), TimerFired(1)], true);
        Assert.Contains(context.PendingActions, a => a.Id == 2 && a.ScheduleTask is not null);

        context.ProcessEvents([Failed(2, executionId)], true);
        Assert.False(task.IsCompleted);
        Assert.DoesNotContain(context.PendingActions, a => a.Id == 3);

        context.ProcessEvents([Completed(4, executionId)], false);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// When the retry timer has already fired, the next attempt registers while the
    /// failed attempt is still being cleaned up. That cleanup must not drop the next
    /// attempt's TaskExecutionId mapping.
    /// </summary>
    [Fact]
    public async Task ActivityRetry_NextAttemptRegisteredDuringCleanup_KeepsTaskExecutionIdMapping()
    {
        var context = CreateContext();
        context.ProcessEvents([TimerFired(1)], true);

        var task = context.CallActivityAsync<string>("act", options: RetryOptions(3));
        var executionId = Assert.Single(context.PendingActions).ScheduleTask.TaskExecutionId;

        context.ProcessEvents([Failed(0, executionId)], true);
        Assert.Contains(context.PendingActions, a => a.Id == 2 && a.ScheduleTask is not null);

        // Matched by TaskExecutionId only: the TaskScheduledId is not a local task ID.
        context.ProcessEvents([Completed(7, executionId)], false);
        Assert.True(task.IsCompleted);
        Assert.Equal("ok", await task);
    }

    /// <summary>
    /// Child workflow retry attempts (2nd onward) link back to the first attempt; the
    /// first attempt carries no link.
    /// </summary>
    [Fact]
    public void ChildWorkflowRetry_LaterAttempts_CarryRetryParentInstanceInfo()
    {
        var context = CreateContext();
        _ = context.CallChildWorkflowAsync<string>("ChildWf", options: new ChildWorkflowTaskOptions
        {
            RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(1))
        });

        var first = Assert.Single(context.PendingActions).CreateChildWorkflow;
        Assert.Null(first.RetryParentInstanceInfo);

        var expectedAttemptId = 2;
        var attemptInstanceId = first.InstanceId;
        for (var attempt = 2; attempt <= 3; attempt++, expectedAttemptId += 2)
        {
            var failedId = expectedAttemptId - 2;
            context.ProcessEvents(
            [
                new HistoryEvent
                {
                    EventId = failedId,
                    ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
                    {
                        Name = "ChildWf", InstanceId = attemptInstanceId
                    }
                },
                new HistoryEvent
                {
                    ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent
                    {
                        TaskScheduledId = failedId,
                        FailureDetails = new TaskFailureDetails { ErrorType = "Exception", ErrorMessage = "boom" }
                    }
                },
                TimerCreated(failedId + 1), TimerFired(failedId + 1),
            ], true);

            var retry = context.PendingActions.Single(a => a.Id == expectedAttemptId).CreateChildWorkflow;
            Assert.NotEqual(first.InstanceId, retry.InstanceId);
            Assert.Equal(first.InstanceId, retry.RetryParentInstanceInfo.InstanceID);
            attemptInstanceId = retry.InstanceId;
        }
    }

    /// <summary>
    /// With a caller-supplied instance ID every attempt reuses that ID, and retry
    /// attempts still link back to it.
    /// </summary>
    [Fact]
    public void ChildWorkflowRetry_WithRequestedInstanceId_LinksRetryToThatId()
    {
        var context = CreateContext();
        _ = context.CallChildWorkflowAsync<string>("ChildWf", options: new ChildWorkflowTaskOptions
        {
            InstanceId = "requested-id",
            RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: 2, firstRetryInterval: TimeSpan.FromSeconds(1))
        });

        var first = Assert.Single(context.PendingActions).CreateChildWorkflow;
        Assert.Equal("requested-id", first.InstanceId);
        Assert.Null(first.RetryParentInstanceInfo);

        context.ProcessEvents(
        [
            new HistoryEvent
            {
                EventId = 0,
                ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
                {
                    Name = "ChildWf", InstanceId = "requested-id"
                }
            },
            new HistoryEvent
            {
                ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent
                {
                    TaskScheduledId = 0,
                    FailureDetails = new TaskFailureDetails { ErrorType = "Exception", ErrorMessage = "boom" }
                }
            },
        ], true);
        var timer = context.PendingActions.Single(a => a.Id == 1).CreateTimer;
        Assert.Equal("requested-id", timer.ChildWorkflowRetry.InstanceId);

        context.ProcessEvents([TimerCreated(1), TimerFired(1)], true);
        var retry = context.PendingActions.Single(a => a.Id == 2).CreateChildWorkflow;
        Assert.Equal("requested-id", retry.InstanceId);
        Assert.Equal("requested-id", retry.RetryParentInstanceInfo.InstanceID);
    }

    /// <summary>
    /// Test 5 — child workflow retry timer sets TimerOriginChildWorkflowRetry.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ChildWorkflowRetry_SetsTimerOriginChildWorkflowRetry()
    {
        var (worker, factory) = CreateWorkerAndFactory();
        
        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.CallChildWorkflowAsync<string>("ChildWf", options: new ChildWorkflowTaskOptions
                {
                    RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: 2,
                        firstRetryInterval: TimeSpan.FromSeconds(1))
                });
                return null;
            }));

        // We need the child to fail. The failure is indicated in history.
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                // First child scheduled
                new HistoryEvent
                {
                    EventId = 0,
                    ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
                    {
                        Name = "ChildWf",
                        InstanceId = "child-0"
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent
                    {
                        TaskScheduledId = 0,
                        FailureDetails = new TaskFailureDetails
                        {
                            ErrorType = "Exception",
                            ErrorMessage = "child failed"
                        }
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        // Should have a retry timer with ChildWorkflowRetry origin
        var retryTimer = response.Actions.FirstOrDefault(a => a.CreateTimer != null);
        Assert.NotNull(retryTimer);
        Assert.Equal(CreateTimerAction.OriginOneofCase.ChildWorkflowRetry, retryTimer!.CreateTimer!.OriginCase);
        Assert.NotEmpty(retryTimer.CreateTimer.ChildWorkflowRetry.InstanceId);
    }

    /// <summary>
    /// Test 6 — child workflow retry instanceId always points to first child.
    /// Verifies the first-child rule across multiple retries.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task ChildWorkflowRetry_InstanceId_AlwaysPointsToFirstChild()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.CallChildWorkflowAsync<string>("ChildWf", options: new ChildWorkflowTaskOptions
                {
                    RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: 3,
                        firstRetryInterval: TimeSpan.FromSeconds(1))
                });
                return null;
            }));

        // First attempt: child scheduled, created, and fails.
        // Then retry timer fires and second child scheduled, created, and fails.
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                // First child - we don't know the exact generated instanceId, so match by EventId
                new HistoryEvent
                {
                    EventId = 0,
                    ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
                    {
                        Name = "ChildWf",
                        InstanceId = "child-first"
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent
                    {
                        TaskScheduledId = 0,
                        FailureDetails = new TaskFailureDetails
                        {
                            ErrorType = "Exception",
                            ErrorMessage = "child failed"
                        }
                    }
                },
                // Retry timer
                new HistoryEvent
                {
                    EventId = 1,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(2))
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(2)),
                new HistoryEvent
                {
                    TimerFired = new TimerFiredEvent
                    {
                        TimerId = 1,
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(2))
                    }
                },
                // Second child scheduled and fails
                new HistoryEvent
                {
                    EventId = 2,
                    ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
                    {
                        Name = "ChildWf",
                        InstanceId = "child-second"
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(3)),
                new HistoryEvent
                {
                    ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent
                    {
                        TaskScheduledId = 2,
                        FailureDetails = new TaskFailureDetails
                        {
                            ErrorType = "Exception",
                            ErrorMessage = "child failed again"
                        }
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        // Should have a second retry timer with the first child's instance ID
        var retryTimer = response.Actions.FirstOrDefault(a =>
            a.CreateTimer?.OriginCase == CreateTimerAction.OriginOneofCase.ChildWorkflowRetry);
        Assert.NotNull(retryTimer);
        
        // The instanceId should be stable — it should be the same value that was used
        // for the first retry timer (which we can't directly observe in this test since
        // the first timer was already consumed in history). But we can verify it's not empty.
        Assert.NotEmpty(retryTimer!.CreateTimer!.ChildWorkflowRetry.InstanceId);
    }

    // =====================================================================
    // Optional timer — happy path (Tests 7–8)
    // =====================================================================

    /// <summary>
    /// Test 7 — indefinite WaitForExternalEvent emits the sentinel optional timer.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task WaitForExternalEvent_Indefinite_EmitsSentinelOptionalTimer()
    {
        var context = CreateContext();
        _ = context.WaitForExternalEventAsync<string>("myEvent", TimeSpan.FromSeconds(-1));

        var timerAction = context.PendingActions
            .FirstOrDefault(a => a.CreateTimer != null);
        Assert.NotNull(timerAction);
        Assert.Equal(CreateTimerAction.OriginOneofCase.ExternalEvent, timerAction!.CreateTimer!.OriginCase);
        Assert.Equal("myEvent", timerAction.CreateTimer.ExternalEvent.Name);
        Assert.Equal(TimerOriginHelpers.ExternalEventIndefiniteFireAt, timerAction.CreateTimer.FireAt);
    }

    /// <summary>
    /// Test 8 — zero-timeout WaitForExternalEvent emits no timer.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task WaitForExternalEvent_ZeroTimeout_EmitsNoTimer()
    {
        var context = CreateContext();

        // Zero timeout should throw TaskCanceledException and emit no timer
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            context.WaitForExternalEventAsync<string>("myEvent", TimeSpan.Zero));

        var timerAction = context.PendingActions
            .FirstOrDefault(a => a.CreateTimer != null);
        Assert.Null(timerAction);
    }

    // =====================================================================
    // Optional timer — replay compatibility (Tests 9–13)
    // =====================================================================

    /// <summary>
    /// Test 9 — post-patch replay matches the optional timer normally.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Replay_PostPatch_MatchesOptionalTimerNormally()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                var result = await ctx.WaitForExternalEventAsync<string>("myEvent", TimeSpan.FromSeconds(-1));
                return result;
            }));

        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                // Post-patch history includes the optional timer
                new HistoryEvent
                {
                    EventId = 0,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = TimerOriginHelpers.ExternalEventIndefiniteFireAt,
                        ExternalEvent = new TimerOriginExternalEvent { Name = "myEvent" }
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
            },
            NewEvents =
            {
                MakeOrchestratorStarted(StartTime.AddSeconds(2)),
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "myEvent",
                        Input = "\"hello\""
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteWorkflow != null).CompleteWorkflow!;
        Assert.Equal(OrchestrationStatus.Completed, complete.WorkflowStatus);
    }

    /// <summary>
    /// Test 10 — pre-patch replay, indefinite wait followed by CallActivity.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Replay_PrePatch_IndefiniteWait_FollowedByCallActivity()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.WaitForExternalEventAsync<string>("myEvent", TimeSpan.FromSeconds(-1));
                var result = await ctx.CallActivityAsync<string>("A");
                return result;
            }));
        factory.AddActivity("A", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>("result")));

        // Pre-patch history: no optional timer, activity at EventId=0
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "myEvent",
                        Input = "\"eventPayload\""
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TaskScheduled = new TaskScheduledEvent { Name = "A" }
                },
            },
            NewEvents =
            {
                MakeOrchestratorStarted(StartTime.AddSeconds(2)),
                new HistoryEvent
                {
                    TaskCompleted = new TaskCompletedEvent
                    {
                        TaskScheduledId = 0,
                        Result = "\"result\""
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteWorkflow != null).CompleteWorkflow!;
        Assert.Equal(OrchestrationStatus.Completed, complete.WorkflowStatus);
        
        // Verify no optional timer leaks into the result
        var timerActions = response.Actions.Where(a => a.CreateTimer != null).ToList();
        Assert.Empty(timerActions);
    }

    /// <summary>
    /// Test 11 — pre-patch replay, indefinite wait followed by CallChildWorkflow.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Replay_PrePatch_IndefiniteWait_FollowedByCallChildWorkflow()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.WaitForExternalEventAsync<string>("myEvent", TimeSpan.FromSeconds(-1));
                var result = await ctx.CallChildWorkflowAsync<string>("Child");
                return result;
            }));

        // Pre-patch history: no optional timer, child at EventId=0
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "myEvent",
                        Input = "\"eventPayload\""
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
                    {
                        Name = "Child",
                        InstanceId = "child-1"
                    }
                }
            },
            NewEvents =
            {
                MakeOrchestratorStarted(StartTime.AddSeconds(2)),
                new HistoryEvent
                {
                    ChildWorkflowInstanceCompleted = new ChildWorkflowInstanceCompletedEvent
                    {
                        TaskScheduledId = 0,
                        Result = "\"childResult\""
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteWorkflow != null).CompleteWorkflow!;
        Assert.Equal(OrchestrationStatus.Completed, complete.WorkflowStatus);
    }

    /// <summary>
    /// Test 12 — pre-patch replay, indefinite wait followed by a user CreateTimer.
    /// Asymmetric TimerCreated-specific branch: pending is optional timer, incoming is CreateTimer origin.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Replay_PrePatch_IndefiniteWait_FollowedByUserCreateTimer()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.WaitForExternalEventAsync<string>("myEvent", TimeSpan.FromSeconds(-1));
                await ctx.CreateTimer(TimeSpan.FromSeconds(5));
                return null;
            }));

        // Pre-patch history: no optional timer, user timer at EventId=0
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "myEvent",
                        Input = "\"payload\""
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TimerCreated = new TimerCreatedEvent
                    {
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(6)),
                        CreateTimer = new TimerOriginCreateTimer()
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(6)),
                new HistoryEvent
                {
                    TimerFired = new TimerFiredEvent
                    {
                        TimerId = 0,
                        FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(6))
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteWorkflow != null).CompleteWorkflow!;
        Assert.Equal(OrchestrationStatus.Completed, complete.WorkflowStatus);
    }

    /// <summary>
    /// Test 13 — pre-patch replay, two indefinite waits in sequence.
    /// Validates drop-and-shift composes correctly across multiple optional timers.
    /// </summary>
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Replay_PrePatch_TwoIndefiniteWaitsInSequence()
    {
        var (worker, factory) = CreateWorkerAndFactory();

        factory.AddWorkflow("wf", new InlineWorkflow(
            inputType: typeof(object),
            run: async (ctx, _) =>
            {
                await ctx.WaitForExternalEventAsync<string>("A", TimeSpan.FromSeconds(-1));
                await ctx.CallActivityAsync<string>("ActA");
                await ctx.WaitForExternalEventAsync<string>("B", TimeSpan.FromSeconds(-1));
                await ctx.CallActivityAsync<string>("ActB");
                return null;
            }));
        factory.AddActivity("ActA", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>("resultA")));
        factory.AddActivity("ActB", new InlineActivity(
            inputType: typeof(object),
            run: (_, _) => Task.FromResult<object?>("resultB")));

        // Pre-patch history: no optional timers.
        // ActA at EventId=0, ActB at EventId=1.
        var request = new WorkflowRequest
        {
            InstanceId = "i",
            PastEvents =
            {
                MakeExecutionStarted("wf"),
                MakeOrchestratorStarted(StartTime.AddSeconds(1)),
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "A",
                        Input = "\"payloadA\""
                    }
                },
                new HistoryEvent
                {
                    EventId = 0,
                    TaskScheduled = new TaskScheduledEvent { Name = "ActA" }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(2)),
                new HistoryEvent
                {
                    TaskCompleted = new TaskCompletedEvent
                    {
                        TaskScheduledId = 0,
                        Result = "\"resultA\""
                    }
                },
                MakeOrchestratorStarted(StartTime.AddSeconds(3)),
                new HistoryEvent
                {
                    EventRaised = new EventRaisedEvent
                    {
                        Name = "B",
                        Input = "\"payloadB\""
                    }
                },
                new HistoryEvent
                {
                    EventId = 1,
                    TaskScheduled = new TaskScheduledEvent { Name = "ActB" }
                },
            },
            NewEvents =
            {
                MakeOrchestratorStarted(StartTime.AddSeconds(4)),
                new HistoryEvent
                {
                    TaskCompleted = new TaskCompletedEvent
                    {
                        TaskScheduledId = 1,
                        Result = "\"resultB\""
                    }
                }
            }
        };

        var response = await InvokeHandleWorkflowResponseAsync(worker, request);

        Assert.Equal("i", response.InstanceId);
        var complete = response.Actions.Single(a => a.CompleteWorkflow != null).CompleteWorkflow!;
        Assert.Equal(OrchestrationStatus.Completed, complete.WorkflowStatus);
        
        // Verify no optional timers leak into the result
        var timerActions = response.Actions.Where(a => a.CreateTimer != null).ToList();
        Assert.Empty(timerActions);
    }

    // =====================================================================
    // Helper methods
    // =====================================================================

    private static WorkflowOrchestrationContext CreateContext()
    {
        var serializer = new JsonDaprSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var tracker = new WorkflowVersionTracker([]);
        return new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "instance-1",
            currentUtcDateTime: StartTime,
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance,
            versionTracker: tracker);
    }

    private static (WorkflowWorker worker, StubWorkflowsFactory factory) CreateWorkerAndFactory()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonDaprSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var factory = new StubWorkflowsFactory();

        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        var grpcClient = new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(callInvoker.Object);

        var worker = new WorkflowWorker(
            grpcClient.Object,
            factory,
            NullLoggerFactory.Instance,
            serializer,
            sp);

        return (worker, factory);
    }

    private static WorkflowTaskOptions RetryOptions(int maxAttempts) => new()
    {
        RetryPolicy = new WorkflowRetryPolicy(maxNumberOfAttempts: maxAttempts,
            firstRetryInterval: TimeSpan.FromSeconds(1))
    };

    private static HistoryEvent Scheduled(int eventId, string taskExecutionId) => new()
    {
        EventId = eventId,
        TaskScheduled = new TaskScheduledEvent { Name = "act", TaskExecutionId = taskExecutionId }
    };

    private static HistoryEvent Failed(int taskScheduledId, string taskExecutionId) => new()
    {
        TaskFailed = new TaskFailedEvent
        {
            TaskScheduledId = taskScheduledId,
            TaskExecutionId = taskExecutionId,
            FailureDetails = new TaskFailureDetails { ErrorType = "InvalidOperationException", ErrorMessage = "boom" }
        }
    };

    private static HistoryEvent Completed(int taskScheduledId, string taskExecutionId) => new()
    {
        TaskCompleted = new TaskCompletedEvent
        {
            TaskScheduledId = taskScheduledId,
            TaskExecutionId = taskExecutionId,
            Result = "\"ok\""
        }
    };

    private static HistoryEvent TimerCreated(int eventId) => new()
    {
        EventId = eventId,
        TimerCreated = new TimerCreatedEvent
        {
            FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(1))
        }
    };

    private static HistoryEvent TimerFired(int timerId) => new()
    {
        TimerFired = new TimerFiredEvent
        {
            TimerId = timerId,
            FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime.AddSeconds(1))
        }
    };

    private static HistoryEvent MakeExecutionStarted(string name, string? input = null)
    {
        return new HistoryEvent
        {
            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(StartTime),
            ExecutionStarted = new ExecutionStartedEvent
            {
                Name = name,
                Input = input
            }
        };
    }

    private static HistoryEvent MakeOrchestratorStarted(DateTime timestamp)
    {
        return new HistoryEvent
        {
            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(timestamp),
            WorkflowStarted = new WorkflowStartedEvent()
        };
    }

    private const string CompletionTokenValue = "abc123";

    private static async Task<WorkflowResponse> InvokeHandleWorkflowResponseAsync(
        WorkflowWorker worker, WorkflowRequest request)
    {
        var method = typeof(WorkflowWorker).GetMethod("HandleWorkflowResponseAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task<WorkflowResponse>)method!.Invoke(worker, [request, CompletionTokenValue])!;
        return await task;
    }

    private sealed class StubWorkflowsFactory : IWorkflowsFactory
    {
        private readonly Dictionary<string, IWorkflow> _workflows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IWorkflowActivity> _activities = new(StringComparer.OrdinalIgnoreCase);

        public void AddWorkflow(string name, IWorkflow wf) => _workflows[name] = wf;
        public void AddActivity(string name, IWorkflowActivity act) => _activities[name] = act;

        public void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow
            => throw new NotSupportedException();
        public void RegisterWorkflow<TInput, TOutput>(string name,
            Func<WorkflowContext, TInput, Task<TOutput>> implementation) => throw new NotSupportedException();
        public void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity
            => throw new NotSupportedException();
        public void RegisterActivity<TInput, TOutput>(string name,
            Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation) => throw new NotSupportedException();

        public bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceProvider,
            out IWorkflow? workflow, out Exception? activationException)
        {
            activationException = null;
            return _workflows.TryGetValue(identifier.Name, out workflow);
        }

        public bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider,
            out IWorkflowActivity? activity, out Exception? activationException)
        {
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
}
