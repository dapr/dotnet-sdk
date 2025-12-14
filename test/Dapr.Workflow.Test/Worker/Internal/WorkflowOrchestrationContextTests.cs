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

namespace Dapr.Workflow.Test.Worker.Internal;

public class WorkflowOrchestrationContextTests
{
    [Fact]
    public void CallActivityAsync_ShouldScheduleTaskAction_WhenNotReplaying()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            history: [],
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var task = context.CallActivityAsync<string>("DoWork", input: new { Value = 3 });

        Assert.NotNull(task);
        Assert.Single(context.PendingActions);

        var action = context.PendingActions[0];
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
                TaskCompleted = new TaskCompletedEvent { Result = "\"hello\"" }
            }
        };

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            history: history,
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var result = await context.CallActivityAsync<string>("Any");

        Assert.Equal("hello", result);
        Assert.Empty(context.PendingActions);
    }

    [Fact]
    public async Task CallActivityAsync_ShouldThrowWorkflowTaskFailedException_FromHistoryTaskFailed()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var history = new[]
        {
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
            history: history,
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        await Assert.ThrowsAsync<WorkflowTaskFailedException>(() => context.CallActivityAsync<string>("Any"));
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
            history: history,
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        var value = await context.WaitForExternalEventAsync<int>("myevent");

        Assert.Equal(123, value);
    }

    [Fact]
    public void SendEvent_ShouldAddSendEventAction_WithSerializedPayload()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var context = new WorkflowOrchestrationContext(
            name: "wf",
            instanceId: "i",
            history: [],
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        context.SendEvent("child-1", "evt", new { A = 1 });

        Assert.Single(context.PendingActions);
        var action = context.PendingActions[0];

        Assert.NotNull(action.SendEvent);
        Assert.Equal("evt", action.SendEvent.Name);
        Assert.Equal("child-1", action.SendEvent.Instance.InstanceId);
        Assert.Contains("\"a\":1", action.SendEvent.Data);
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
            history: history,
            currentUtcDateTime: new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance);

        context.ContinueAsNew(newInput: new { V = 9 }, preserveUnprocessedEvents: true);

        Assert.Single(context.PendingActions);
        var action = context.PendingActions[0];

        Assert.NotNull(action.CompleteOrchestration);
        Assert.Equal(OrchestrationStatus.ContinuedAsNew, action.CompleteOrchestration.OrchestrationStatus);
        Assert.Contains("\"v\":9", action.CompleteOrchestration.Result);
        Assert.Equal(2, action.CompleteOrchestration.CarryoverEvents.Count);
    }

    [Fact]
    public void NewGuid_ShouldBeDeterministic_ForSameInstanceIdAndTimestamp()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var now = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        var c1 = new WorkflowOrchestrationContext("wf", "00000000-0000-0000-0000-000000000001",
            [], now, serializer, NullLoggerFactory.Instance);

        var c2 = new WorkflowOrchestrationContext("wf", "00000000-0000-0000-0000-000000000001",
            [], now, serializer, NullLoggerFactory.Instance);

        var g1 = c1.NewGuid();
        var g2 = c2.NewGuid();

        Assert.Equal(g1, g2);
    }
}
