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
// ------------------------------------------------------------------------

using System.Text.Json;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker.Internal;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Workflow.Test.Worker.Internal;

/// <summary>
/// Tests for workflow history propagation: the SDK API surface for declaring
/// a propagation scope on <see cref="WorkflowTaskOptions"/> /
/// <see cref="ChildWorkflowTaskOptions"/>, propagating the scope to the
/// outgoing <see cref="ScheduleTaskAction"/> / <see cref="CreateChildWorkflowAction"/>,
/// and exposing inbound propagated history through
/// <see cref="WorkflowContext.GetPropagatedHistory"/> as <see cref="PropagatedHistoryEvent"/>
/// values, each carrying typed <see cref="PropagatedHistoryActivityResult"/> /
/// <see cref="PropagatedHistoryWorkflowResult"/> records.
/// </summary>
public class WorkflowHistoryPropagationTests
{
    private static readonly DateTime StartTime = new(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);

    private static WorkflowOrchestrationContext CreateContext(
        string name = "TestWorkflow",
        string instanceId = "instance-1",
        string? appId = null,
        IReadOnlyList<HistoryEvent>? ownHistory = null,
        IEnumerable<PropagatedHistoryChunk>? incomingPropagatedHistory = null)
    {
        var serializer = new JsonDaprSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var tracker = new WorkflowVersionTracker([]);
        return new WorkflowOrchestrationContext(
            name: name,
            instanceId: instanceId,
            currentUtcDateTime: StartTime,
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance,
            versionTracker: tracker,
            appId: appId,
            ownHistory: ownHistory,
            incomingPropagatedHistory: incomingPropagatedHistory);
    }

    private static HistoryEvent MakeEvent(int eventId, Action<HistoryEvent> configure, DateTime? timestamp = null)
    {
        var ev = new HistoryEvent
        {
            EventId = eventId,
            Timestamp = Timestamp.FromDateTime(timestamp ?? StartTime),
        };
        configure(ev);
        return ev;
    }

    private static PropagatedHistoryChunk MakeChunk(string appId, string instanceId, string workflowName,
        params HistoryEvent[] events)
    {
        var chunk = new PropagatedHistoryChunk
        {
            AppId = appId,
            InstanceId = instanceId,
            WorkflowName = workflowName,
        };
        foreach (var ev in events)
        {
            chunk.RawEvents.Add(ev.ToByteString());
        }
        return chunk;
    }

    private static HistoryEvent TaskScheduled(int eventId, string name, string? input = null) =>
        MakeEvent(eventId, e => e.TaskScheduled = new TaskScheduledEvent
        {
            Name = name,
            Input = input,
        });

    private static HistoryEvent TaskCompleted(int eventId, int scheduledId, string? result = null) =>
        MakeEvent(eventId, e => e.TaskCompleted = new TaskCompletedEvent
        {
            TaskScheduledId = scheduledId,
            Result = result,
        });

    private static HistoryEvent TaskFailed(int eventId, int scheduledId, string errorMessage) =>
        MakeEvent(eventId, e => e.TaskFailed = new TaskFailedEvent
        {
            TaskScheduledId = scheduledId,
            FailureDetails = new TaskFailureDetails
            {
                ErrorType = "TestException",
                ErrorMessage = errorMessage,
            },
        });

    private static HistoryEvent ChildCreated(int eventId, string name) =>
        MakeEvent(eventId, e => e.ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent
        {
            Name = name,
        });

    private static HistoryEvent ChildCompleted(int eventId, int creationId, string? result = null) =>
        MakeEvent(eventId, e => e.ChildWorkflowInstanceCompleted = new ChildWorkflowInstanceCompletedEvent
        {
            TaskScheduledId = creationId,
            Result = result,
        });

    private static HistoryEvent ChildFailed(int eventId, int creationId, string errorMessage) =>
        MakeEvent(eventId, e => e.ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent
        {
            TaskScheduledId = creationId,
            FailureDetails = new TaskFailureDetails
            {
                ErrorType = "TestException",
                ErrorMessage = errorMessage,
            },
        });

    // ------------------------------------------------------------------
    //  GetPropagatedHistory — entry shape and ordering
    // ------------------------------------------------------------------

    [Fact]
    public void GetPropagatedHistory_ReturnsNull_WhenNoHistoryPropagated()
    {
        var context = CreateContext();
        Assert.Null(context.GetPropagatedHistory());
    }

    [Fact]
    public void GetPropagatedHistory_ReturnsNull_WhenEmptyChunksProvided()
    {
        var context = CreateContext(incomingPropagatedHistory: []);
        Assert.Null(context.GetPropagatedHistory());
    }

    [Fact]
    public void GetPropagatedHistory_ReturnsSingleWorkflow_WhenOneEntryPropagated()
    {
        var chunk = MakeChunk("parent-app", "parent-instance", "ParentWorkflow",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "ParentWorkflow" }));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);

        var history = context.GetPropagatedHistory();

        Assert.NotNull(history);
        var entries = history.Events;
        Assert.Single(entries);
        Assert.Equal("parent-app", entries[0].AppId);
        Assert.Equal("parent-instance", entries[0].InstanceId);
        Assert.Equal("ParentWorkflow", entries[0].Name);
        Assert.Empty(entries[0].Activities);
        Assert.Empty(entries[0].Workflows);
    }

    [Fact]
    public void GetPropagatedHistory_PreservesEntryOrder()
    {
        // Entries arrive oldest-first: grandparent at index 0, immediate parent last.
        var grandparent = MakeChunk("gp-app", "gp-inst", "GrandparentWf",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "GrandparentWf" }));
        var parent = MakeChunk("p-app", "p-inst", "ParentWf",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "ParentWf" }));

        var context = CreateContext(incomingPropagatedHistory: [grandparent, parent]);
        var history = context.GetPropagatedHistory();

        Assert.NotNull(history);
        var entries = history.Events;
        Assert.Equal(2, entries.Count);
        Assert.Equal("gp-inst", entries[0].InstanceId);
        Assert.Equal("p-inst", entries[1].InstanceId);
    }

    [Fact]
    public void GetPropagatedHistory_ThrowsOnMalformedRawEvents()
    {
        // A runtime sending unparseable proto bytes is a contract violation; surface it
        // instead of silently masking the bug.
        var chunk = new PropagatedHistoryChunk
        {
            AppId = "app",
            InstanceId = "inst",
            WorkflowName = "Wf",
        };
        chunk.RawEvents.Add(ByteString.CopyFrom([0xff, 0xff, 0xff, 0xff, 0xff]));

        Assert.Throws<InvalidProtocolBufferException>(() => CreateContext(incomingPropagatedHistory: [chunk]));
    }

    // ------------------------------------------------------------------
    //  Activity resolution from raw events
    // ------------------------------------------------------------------

    [Fact]
    public void Activity_ResolvedAs_CompletedWithInputAndOutput()
    {
        var chunk = MakeChunk("app", "inst", "Wf",
            TaskScheduled(eventId: 1, name: "ValidateMerchant", input: "\"merchant-1\""),
            TaskCompleted(eventId: 2, scheduledId: 1, result: "true"));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));
        Assert.True(workflow.TryGetLastActivityByName("ValidateMerchant", out var activity));

        Assert.Equal("ValidateMerchant", activity.Name);
        Assert.Equal(PropagatedHistoryStatus.Completed, activity.Status);
        Assert.Equal("\"merchant-1\"", activity.Input);
        Assert.Equal("true", activity.Output);
        Assert.Null(activity.FailureDetails);
    }

    [Fact]
    public void Activity_ResolvedAs_FailedWithFailureDetails()
    {
        var chunk = MakeChunk("app", "inst", "Wf",
            TaskScheduled(eventId: 1, name: "ValidateCard"),
            TaskFailed(eventId: 2, scheduledId: 1, errorMessage: "card declined"));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));
        Assert.True(workflow.TryGetLastActivityByName("ValidateCard", out var activity));

        Assert.Equal(PropagatedHistoryStatus.Failed, activity.Status);
        Assert.NotNull(activity.FailureDetails);
        Assert.Equal("card declined", activity.FailureDetails.ErrorMessage);
    }

    [Fact]
    public void Activity_ResolvedAs_StartedOnly_WhenNotYetCompleted()
    {
        var chunk = MakeChunk("app", "inst", "Wf",
            TaskScheduled(eventId: 1, name: "PendingCheck", input: "\"in\""));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));
        Assert.True(workflow.TryGetLastActivityByName("PendingCheck", out var activity));

        Assert.Equal(PropagatedHistoryStatus.Pending, activity.Status);
        Assert.Equal("\"in\"", activity.Input);
        Assert.Null(activity.Output);
    }

    [Fact]
    public void TryGetLastActivityByName_ReturnsMostRecentInvocation_WhenRetried()
    {
        // Same activity scheduled twice — first completes, second fails. TryGet returns the failed (most recent).
        var chunk = MakeChunk("app", "inst", "Wf",
            TaskScheduled(eventId: 1, name: "ValidateCard", input: "\"first\""),
            TaskCompleted(eventId: 2, scheduledId: 1, result: "true"),
            TaskScheduled(eventId: 3, name: "ValidateCard", input: "\"second\""),
            TaskFailed(eventId: 4, scheduledId: 3, errorMessage: "card declined"));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));

        var all = workflow.GetActivitiesByName("ValidateCard");
        Assert.Equal(2, all.Count);
        Assert.Equal(PropagatedHistoryStatus.Completed, all[0].Status);
        Assert.Equal(PropagatedHistoryStatus.Failed, all[1].Status);

        Assert.True(workflow.TryGetLastActivityByName("ValidateCard", out var last));
        Assert.Equal(PropagatedHistoryStatus.Failed, last.Status);
        Assert.Equal("card declined", last.FailureDetails!.ErrorMessage);
    }

    [Fact]
    public void TryGetLastActivityByName_ReturnsFalse_WhenMissing()
    {
        var chunk = MakeChunk("app", "inst", "Wf",
            TaskScheduled(eventId: 1, name: "Real"));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));

        Assert.False(workflow.TryGetLastActivityByName("Missing", out var missing));
        Assert.Null(missing);
    }

    // ------------------------------------------------------------------
    //  Child workflow resolution
    // ------------------------------------------------------------------

    [Fact]
    public void ChildWorkflow_ResolvedAs_Completed()
    {
        var chunk = MakeChunk("app", "inst", "Wf",
            ChildCreated(eventId: 1, name: "ProcessPayment"),
            ChildCompleted(eventId: 2, creationId: 1, result: "\"paid\""));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));
        Assert.True(workflow.TryGetLastWorkflowByName("ProcessPayment", out var child));

        Assert.Equal(PropagatedHistoryStatus.Completed, child.Status);
        Assert.Equal("\"paid\"", child.Output);
    }

    [Fact]
    public void ChildWorkflow_ResolvedAs_Failed()
    {
        var chunk = MakeChunk("app", "inst", "Wf",
            ChildCreated(eventId: 1, name: "ProcessPayment"),
            ChildFailed(eventId: 2, creationId: 1, errorMessage: "boom"));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));
        Assert.True(workflow.TryGetLastWorkflowByName("ProcessPayment", out var child));

        Assert.Equal(PropagatedHistoryStatus.Failed, child.Status);
        Assert.Equal("boom", child.FailureDetails!.ErrorMessage);
    }

    [Fact]
    public void TryGetLastChildWorkflowByName_ReturnsFalse_WhenMissing()
    {
        var chunk = MakeChunk("app", "inst", "Wf");
        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        Assert.True(context.GetPropagatedHistory()!.TryGetLastWorkflowEventByName("Wf", out var workflow));

        Assert.False(workflow.TryGetLastWorkflowByName("Missing", out var missing));
        Assert.Null(missing);
    }

    // ------------------------------------------------------------------
    //  PropagatedHistory-level helpers
    // ------------------------------------------------------------------

    [Fact]
    public void GetAppIds_ReturnsOrderedDeduplicatedList()
    {
        var history = new PropagatedHistory([
            new PropagatedHistoryEvent("i1", "appA", "WfA", [], []),
            new PropagatedHistoryEvent("i2", "appB", "WfB", [], []),
            new PropagatedHistoryEvent("i3", "appA", "WfA2", [], []),
        ]);

        Assert.Equal(["appA", "appB"], history.GetAppIds());
    }

    [Fact]
    public void TryGetLastWorkflowByName_ReturnsMostRecent_WhenNameRepeated()
    {
        var history = new PropagatedHistory([
            new PropagatedHistoryEvent("wf-1", "app", "Loop", [], []),
            new PropagatedHistoryEvent("wf-2", "app", "Loop", [], []),
        ]);

        Assert.True(history.TryGetLastWorkflowEventByName("Loop", out var last));
        Assert.Equal("wf-2", last.InstanceId);
        Assert.Equal(2, history.GetEventsByWorkflowName("Loop").Count);
    }

    [Fact]
    public void TryGetLastWorkflowByName_ReturnsFalse_WhenMissing()
    {
        var history = new PropagatedHistory([]);
        Assert.False(history.TryGetLastWorkflowEventByName("Missing", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void PropagatedHistory_Ctor_ThrowsOnNullEntries()
    {
        Assert.Throws<ArgumentNullException>(() => new PropagatedHistory(null!));
    }

    [Fact]
    public void FilterByInstanceId_ReturnsMatchingEntries_WhenInstanceIdMatches()
    {
        var history = new PropagatedHistory([
            new PropagatedHistoryEvent("inst-a", "app", "WfA", [], []),
            new PropagatedHistoryEvent("inst-b", "app", "WfB", [], []),
            new PropagatedHistoryEvent("inst-c", "app", "WfC", [], []),
        ]);

        var result = history.FilterByInstanceId("inst-b");

        Assert.Single(result);
        Assert.Equal("inst-b", result[0].InstanceId);
    }

    [Fact]
    public void FilterByInstanceId_ReturnsEmptyList_WhenNoMatch()
    {
        var history = new PropagatedHistory([
            new PropagatedHistoryEvent("inst-a", "app", "WfA", [], []),
        ]);

        var result = history.FilterByInstanceId("inst-z");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterByInstanceId_ReturnsBothEntries_WhenSameInstanceAppearsMultipleTimes()
    {
        // ContinueAsNew or replay can produce multiple chunks for the same instance ID.
        var history = new PropagatedHistory([
            new PropagatedHistoryEvent("inst-loop", "app", "LoopWf", [], []),
            new PropagatedHistoryEvent("inst-other", "app", "OtherWf", [], []),
            new PropagatedHistoryEvent("inst-loop", "app", "LoopWf", [], []),
        ]);

        var result = history.FilterByInstanceId("inst-loop");

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("inst-loop", e.InstanceId));
    }

    [Fact]
    public void FilterByInstanceId_IsCaseSensitive()
    {
        // Instance IDs use Ordinal comparison (unlike app/workflow name lookups which are OrdinalIgnoreCase).
        var history = new PropagatedHistory([
            new PropagatedHistoryEvent("Instance-1", "app", "Wf", [], []),
        ]);

        Assert.Empty(history.FilterByInstanceId("instance-1"));
        Assert.Single(history.FilterByInstanceId("Instance-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilterByInstanceId_ThrowsOnNullOrWhitespace(string? instanceId)
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentException>(() => history.FilterByInstanceId(instanceId!));
    }

    [Fact]
    public void PropagatedHistory_NameAndAppIdLookups_AreCaseInsensitive()
    {
        // Workflow / activity names register case-insensitively in WorkflowsFactory,
        // and AppIds are matched case-insensitively elsewhere in the SDK. The propagated
        // history lookups must follow the same contract.
        var activity = new PropagatedHistoryActivityResult(
            Name: "ValidateMerchant", Status: PropagatedHistoryStatus.Completed,
            Input: null, Output: null, FailureDetails: null);
        var child = new PropagatedHistoryWorkflowResult(
            Name: "FraudDetection", Status: PropagatedHistoryStatus.Completed,
            Output: null, FailureDetails: null);
        var entry = new PropagatedHistoryEvent("inst-1", "AppA", "MerchantCheckout", [activity], [child]);
        var history = new PropagatedHistory([
            entry,
            new PropagatedHistoryEvent("inst-2", "appa", "OtherWf", [], []),
        ]);

        // Both entries belong to the same app (differing only in casing), so the de-duped
        // app-id list collapses to one, while a case-insensitive filter matches both entries.
        Assert.Single(history.GetAppIds());
        Assert.Equal(2, history.FilterByAppId("APPA").Count);
        Assert.Single(history.GetEventsByWorkflowName("merchantcheckout"));
        Assert.True(history.TryGetLastWorkflowEventByName("MERCHANTCHECKOUT", out _));
        Assert.Single(entry.GetActivitiesByName("validatemerchant"));
        Assert.True(entry.TryGetLastActivityByName("VALIDATEMERCHANT", out _));
        Assert.Single(entry.GetWorkflowsByName("frauddetection"));
        Assert.True(entry.TryGetLastWorkflowByName("FRAUDDETECTION", out _));
    }

    [Fact]
    public void Status_IsStoredAndReadBackCorrectly()
    {
        var pending = new PropagatedHistoryActivityResult(
            Name: "A", Status: PropagatedHistoryStatus.Pending,
            Input: null, Output: null, FailureDetails: null);
        var completed = pending with { Status = PropagatedHistoryStatus.Completed };
        var failed = pending with { Status = PropagatedHistoryStatus.Failed };

        Assert.Equal(PropagatedHistoryStatus.Pending, pending.Status);
        Assert.Equal(PropagatedHistoryStatus.Completed, completed.Status);
        Assert.Equal(PropagatedHistoryStatus.Failed, failed.Status);

        var child = new PropagatedHistoryWorkflowResult(
            Name: "C", Status: PropagatedHistoryStatus.Completed,
            Output: null, FailureDetails: null);
        Assert.Equal(PropagatedHistoryStatus.Completed, child.Status);
        Assert.Equal(PropagatedHistoryStatus.Failed, (child with { Status = PropagatedHistoryStatus.Failed }).Status);
    }

    // ------------------------------------------------------------------
    //  Scheduling helpers — WithHistoryPropagation
    // ------------------------------------------------------------------

    [Fact]
    public void WorkflowTaskOptions_WithHistoryPropagation_SetsScope()
    {
        var options = new WorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.Lineage);
        Assert.Equal(HistoryPropagationScope.Lineage, options.PropagationScope);
    }

    [Fact]
    public void WorkflowTaskOptions_WithHistoryPropagation_DoesNotMutateOriginal()
    {
        var original = new WorkflowTaskOptions();
        var updated = original.WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

        Assert.Equal(HistoryPropagationScope.None, original.PropagationScope);
        Assert.Equal(HistoryPropagationScope.OwnHistory, updated.PropagationScope);
    }

    [Fact]
    public void ChildWorkflowTaskOptions_WithHistoryPropagation_SetsScope()
    {
        var options = new ChildWorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.Lineage);
        Assert.Equal(HistoryPropagationScope.Lineage, options.PropagationScope);
    }

    [Fact]
    public void ChildWorkflowTaskOptions_WithHistoryPropagation_PreservesOtherFields()
    {
        var original = new ChildWorkflowTaskOptions(InstanceId: "id-1");
        var updated = original.WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

        Assert.Equal(HistoryPropagationScope.None, original.PropagationScope);
        Assert.Equal(HistoryPropagationScope.OwnHistory, updated.PropagationScope);
        Assert.Equal("id-1", updated.InstanceId);
    }

    [Fact]
    public void ChildWorkflowTaskOptions_WithHistoryPropagation_ReturnsDerivedType()
    {
        // Locks in the `new`-hiding behavior on the derived record: invoked on a
        // ChildWorkflowTaskOptions reference, WithHistoryPropagation must return
        // ChildWorkflowTaskOptions (not the base WorkflowTaskOptions) so InstanceId
        // and other derived fields survive the with-expression.
        var original = new ChildWorkflowTaskOptions(InstanceId: "id-1");
        var updated = original.WithHistoryPropagation(HistoryPropagationScope.Lineage);

        Assert.IsType<ChildWorkflowTaskOptions>(updated);
    }

    // ------------------------------------------------------------------
    //  Outbound action scope — activity path
    // ------------------------------------------------------------------

    [Fact]
    public void CallActivityAsync_DefaultScope_LeavesActionScopeUnset()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallActivityAsync<string>("Echo");

        var action = context.PendingActions
            .Select(a => a.ScheduleTask)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.None, action.HistoryPropagationScope);
    }

    [Fact]
    public void CallActivityAsync_WithOwnHistory_SetsScopeOnAction()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallActivityAsync<string>("Echo",
            options: new WorkflowTaskOptions(PropagationScope: HistoryPropagationScope.OwnHistory));

        var action = context.PendingActions
            .Select(a => a.ScheduleTask)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.OwnHistory, action.HistoryPropagationScope);
    }

    [Fact]
    public void CallActivityAsync_WithLineage_SetsScopeOnAction()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallActivityAsync<string>("Echo",
            options: new WorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.Lineage));

        var action = context.PendingActions
            .Select(a => a.ScheduleTask)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.Lineage, action.HistoryPropagationScope);
    }

    // ------------------------------------------------------------------
    //  Outbound action scope — child workflow path
    // ------------------------------------------------------------------

    [Fact]
    public void CallChildWorkflowAsync_DefaultScope_LeavesActionScopeUnset()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallChildWorkflowAsync<string>("ChildWf");

        var action = context.PendingActions
            .Select(a => a.CreateChildWorkflow)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.None, action.HistoryPropagationScope);
    }

    [Fact]
    public void CallChildWorkflowAsync_WithOwnHistory_SetsScopeOnAction()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallChildWorkflowAsync<string>("ChildWf",
            options: new ChildWorkflowTaskOptions(PropagationScope: HistoryPropagationScope.OwnHistory));

        var action = context.PendingActions
            .Select(a => a.CreateChildWorkflow)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.OwnHistory, action.HistoryPropagationScope);
    }

    [Fact]
    public void CallChildWorkflowAsync_WithLineage_SetsScopeOnAction()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallChildWorkflowAsync<string>("ChildWf",
            options: new ChildWorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.Lineage));

        var action = context.PendingActions
            .Select(a => a.CreateChildWorkflow)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.Lineage, action.HistoryPropagationScope);
    }
}
