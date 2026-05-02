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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Versioning;
using Dapr.Workflow.Worker.Internal;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dapr.Workflow.Test.Worker.Internal;

/// <summary>
/// Tests for workflow history propagation via WorkflowOrchestrationContext.
/// </summary>
public class WorkflowHistoryPropagationTests
{
    private static WorkflowOrchestrationContext CreateContext(
        string name = "TestWorkflow",
        string instanceId = "instance-1",
        string? appId = null,
        IReadOnlyList<HistoryEvent>? ownHistory = null,
        IEnumerable<PropagatedHistorySegment>? incomingPropagatedHistory = null)
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var tracker = new WorkflowVersionTracker([]);
        return new WorkflowOrchestrationContext(
            name: name,
            instanceId: instanceId,
            currentUtcDateTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            workflowSerializer: serializer,
            loggerFactory: NullLoggerFactory.Instance,
            versionTracker: tracker,
            appId: appId,
            ownHistory: ownHistory,
            incomingPropagatedHistory: incomingPropagatedHistory);
    }

    // ── GetPropagatedHistory ──────────────────────────────────────────────────

    [Fact]
    public void GetPropagatedHistory_ReturnsNull_WhenNoHistoryPropagated()
    {
        var context = CreateContext();
        Assert.Null(context.GetPropagatedHistory());
    }

    [Fact]
    public void GetPropagatedHistory_ReturnsNull_WhenEmptyPropagatedHistoryProvided()
    {
        var context = CreateContext(incomingPropagatedHistory: []);
        Assert.Null(context.GetPropagatedHistory());
    }

    [Fact]
    public void GetPropagatedHistory_ReturnsSingleEntry_WhenOneSegmentPropagated()
    {
        var segment = new PropagatedHistorySegment
        {
            AppId = "parent-app",
            InstanceId = "parent-instance",
            WorkflowName = "ParentWorkflow"
        };
        segment.Events.Add(MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "ParentWorkflow" }));

        var context = CreateContext(incomingPropagatedHistory: [segment]);

        var history = context.GetPropagatedHistory();

        Assert.NotNull(history);
        Assert.Single(history.Entries);
        var entry = history.Entries[0];
        Assert.Equal("parent-app", entry.AppId);
        Assert.Equal("parent-instance", entry.InstanceId);
        Assert.Equal("ParentWorkflow", entry.WorkflowName);
        Assert.Single(entry.Events);
        Assert.Equal(HistoryEventKind.ExecutionStarted, entry.Events[0].Kind);
        Assert.Equal(1, entry.Events[0].EventId);
    }

    [Fact]
    public void GetPropagatedHistory_ReturnsMultipleEntries_ForLineagePropagation()
    {
        var parent = new PropagatedHistorySegment
        {
            AppId = "app-a", InstanceId = "inst-parent", WorkflowName = "ParentWf"
        };
        var grandparent = new PropagatedHistorySegment
        {
            AppId = "app-b", InstanceId = "inst-grandparent", WorkflowName = "GrandparentWf"
        };

        var context = CreateContext(incomingPropagatedHistory: [parent, grandparent]);

        var history = context.GetPropagatedHistory();

        Assert.NotNull(history);
        Assert.Equal(2, history.Entries.Count);
        Assert.Equal("inst-parent", history.Entries[0].InstanceId);
        Assert.Equal("inst-grandparent", history.Entries[1].InstanceId);
    }

    [Fact]
    public void GetPropagatedHistory_MapsAllKnownEventKinds()
    {
        var seg = new PropagatedHistorySegment { AppId = "app", InstanceId = "id", WorkflowName = "wf" };
        var kindMap = new Dictionary<HistoryEvent, HistoryEventKind>
        {
            { MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent()), HistoryEventKind.ExecutionStarted },
            { MakeEvent(2, e => e.ExecutionCompleted = new ExecutionCompletedEvent()), HistoryEventKind.ExecutionCompleted },
            { MakeEvent(3, e => e.ExecutionTerminated = new ExecutionTerminatedEvent()), HistoryEventKind.ExecutionTerminated },
            { MakeEvent(4, e => e.TaskScheduled = new TaskScheduledEvent { Name = "a" }), HistoryEventKind.TaskScheduled },
            { MakeEvent(5, e => e.TaskCompleted = new TaskCompletedEvent()), HistoryEventKind.TaskCompleted },
            { MakeEvent(6, e => e.TaskFailed = new TaskFailedEvent()), HistoryEventKind.TaskFailed },
            { MakeEvent(7, e => e.SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent()), HistoryEventKind.SubOrchestrationInstanceCreated },
            { MakeEvent(8, e => e.SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent()), HistoryEventKind.SubOrchestrationInstanceCompleted },
            { MakeEvent(9, e => e.SubOrchestrationInstanceFailed = new SubOrchestrationInstanceFailedEvent()), HistoryEventKind.SubOrchestrationInstanceFailed },
            { MakeEvent(10, e => e.TimerCreated = new TimerCreatedEvent()), HistoryEventKind.TimerCreated },
            { MakeEvent(11, e => e.TimerFired = new TimerFiredEvent()), HistoryEventKind.TimerFired },
            { MakeEvent(12, e => e.OrchestratorStarted = new OrchestratorStartedEvent()), HistoryEventKind.OrchestratorStarted },
            { MakeEvent(13, e => e.OrchestratorCompleted = new OrchestratorCompletedEvent()), HistoryEventKind.OrchestratorCompleted },
            { MakeEvent(14, e => e.EventSent = new EventSentEvent()), HistoryEventKind.EventSent },
            { MakeEvent(15, e => e.EventRaised = new EventRaisedEvent()), HistoryEventKind.EventRaised },
            { MakeEvent(16, e => e.ContinueAsNew = new ContinueAsNewEvent()), HistoryEventKind.ContinueAsNew },
            { MakeEvent(17, e => e.ExecutionSuspended = new ExecutionSuspendedEvent()), HistoryEventKind.ExecutionSuspended },
            { MakeEvent(18, e => e.ExecutionResumed = new ExecutionResumedEvent()), HistoryEventKind.ExecutionResumed },
        };

        seg.Events.AddRange(kindMap.Keys);
        var context = CreateContext(incomingPropagatedHistory: [seg]);
        var history = context.GetPropagatedHistory()!;
        var events = history.Entries[0].Events;

        foreach (var (protoEvent, expectedKind) in kindMap)
        {
            var mapped = events.FirstOrDefault(e => e.EventId == protoEvent.EventId);
            Assert.NotNull(mapped);
            Assert.Equal(expectedKind, mapped.Kind);
        }
    }

    [Fact]
    public void GetPropagatedHistory_MapsTimestamp_Correctly()
    {
        var ts = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var protoTs = Timestamp.FromDateTimeOffset(ts);
        var seg = new PropagatedHistorySegment { AppId = "a", InstanceId = "i", WorkflowName = "w" };
        seg.Events.Add(new HistoryEvent
        {
            EventId = 1,
            Timestamp = protoTs,
            ExecutionStarted = new ExecutionStartedEvent()
        });

        var context = CreateContext(incomingPropagatedHistory: [seg]);
        var entry = context.GetPropagatedHistory()!.Entries[0];

        Assert.Equal(ts, entry.Events[0].Timestamp);
    }

    [Fact]
    public void GetPropagatedHistory_MapsUnknownEventType_ToUnknown()
    {
        var seg = new PropagatedHistorySegment { AppId = "a", InstanceId = "i", WorkflowName = "w" };
        // A HistoryEvent with no event type set → Unknown
        seg.Events.Add(new HistoryEvent { EventId = 99 });

        var context = CreateContext(incomingPropagatedHistory: [seg]);
        var events = context.GetPropagatedHistory()!.Entries[0].Events;

        Assert.Equal(HistoryEventKind.Unknown, events[0].Kind);
    }

    // ── PropagatedHistory filtering ───────────────────────────────────────────

    [Fact]
    public void FilterByAppId_ReturnsOnlyMatchingEntries()
    {
        var entries = new[]
        {
            new PropagatedHistoryEntry("app-a", "i1", "WfA", []),
            new PropagatedHistoryEntry("app-b", "i2", "WfB", []),
            new PropagatedHistoryEntry("APP-A", "i3", "WfA2", []),
        };
        var history = new PropagatedHistory(entries);

        var filtered = history.FilterByAppId("app-a");

        // Case-insensitive match
        Assert.Equal(2, filtered.Entries.Count);
        Assert.All(filtered.Entries, e => Assert.Equal("app-a", e.AppId, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void FilterByInstanceId_ReturnsOnlyMatchingEntry()
    {
        var entries = new[]
        {
            new PropagatedHistoryEntry("app", "instance-1", "Wf1", []),
            new PropagatedHistoryEntry("app", "instance-2", "Wf2", []),
        };
        var history = new PropagatedHistory(entries);

        var filtered = history.FilterByInstanceId("instance-1");

        Assert.Single(filtered.Entries);
        Assert.Equal("instance-1", filtered.Entries[0].InstanceId);
    }

    [Fact]
    public void FilterByInstanceId_IsCaseSensitive()
    {
        var entries = new[] { new PropagatedHistoryEntry("app", "Instance-1", "Wf", []) };
        var history = new PropagatedHistory(entries);

        // Exact case match
        Assert.Single(history.FilterByInstanceId("Instance-1").Entries);
        // Different case → no match
        Assert.Empty(history.FilterByInstanceId("instance-1").Entries);
    }

    [Fact]
    public void FilterByWorkflowName_ReturnsOnlyMatchingEntries()
    {
        var entries = new[]
        {
            new PropagatedHistoryEntry("app", "i1", "PaymentWorkflow", []),
            new PropagatedHistoryEntry("app", "i2", "OrderWorkflow", []),
            new PropagatedHistoryEntry("app", "i3", "PaymentWorkflow", []),
        };
        var history = new PropagatedHistory(entries);

        var filtered = history.FilterByWorkflowName("PaymentWorkflow");

        Assert.Equal(2, filtered.Entries.Count);
        Assert.All(filtered.Entries, e => Assert.Equal("PaymentWorkflow", e.WorkflowName));
    }

    [Fact]
    public void FilterByWorkflowName_IsCaseSensitive()
    {
        var entries = new[] { new PropagatedHistoryEntry("app", "i", "PaymentWorkflow", []) };
        var history = new PropagatedHistory(entries);

        Assert.Single(history.FilterByWorkflowName("PaymentWorkflow").Entries);
        Assert.Empty(history.FilterByWorkflowName("paymentworkflow").Entries);
    }

    [Fact]
    public void FilterMethods_ReturnEmptyHistory_WhenNoMatches()
    {
        var history = new PropagatedHistory(
        [
            new PropagatedHistoryEntry("app-a", "i1", "Wf1", [])
        ]);

        Assert.Empty(history.FilterByAppId("app-z").Entries);
        Assert.Empty(history.FilterByInstanceId("no-such-id").Entries);
        Assert.Empty(history.FilterByWorkflowName("NoSuchWf").Entries);
    }

    [Fact]
    public void FilterMethods_ThrowArgumentException_WhenNullOrWhitespace()
    {
        var history = new PropagatedHistory([]);

        // null throws ArgumentNullException (a subclass of ArgumentException)
        Assert.ThrowsAny<ArgumentException>(() => history.FilterByAppId(null!));
        Assert.ThrowsAny<ArgumentException>(() => history.FilterByAppId(""));
        Assert.ThrowsAny<ArgumentException>(() => history.FilterByAppId("   "));

        Assert.ThrowsAny<ArgumentException>(() => history.FilterByInstanceId(null!));
        Assert.ThrowsAny<ArgumentException>(() => history.FilterByInstanceId(""));

        Assert.ThrowsAny<ArgumentException>(() => history.FilterByWorkflowName(null!));
        Assert.ThrowsAny<ArgumentException>(() => history.FilterByWorkflowName(""));
    }

    [Fact]
    public void FilterMethods_CanBeChained()
    {
        var entries = new[]
        {
            new PropagatedHistoryEntry("app-a", "i1", "PaymentWorkflow", []),
            new PropagatedHistoryEntry("app-a", "i2", "OrderWorkflow", []),
            new PropagatedHistoryEntry("app-b", "i3", "PaymentWorkflow", []),
        };
        var history = new PropagatedHistory(entries);

        var filtered = history.FilterByAppId("app-a").FilterByWorkflowName("PaymentWorkflow");

        Assert.Single(filtered.Entries);
        Assert.Equal("i1", filtered.Entries[0].InstanceId);
    }

    // ── ChildWorkflowTaskOptions.WithHistoryPropagation ───────────────────────

    [Fact]
    public void ChildWorkflowTaskOptions_DefaultPropagationScope_IsNone()
    {
        var options = new ChildWorkflowTaskOptions();
        Assert.Equal(HistoryPropagationScope.None, options.PropagationScope);
    }

    [Fact]
    public void WithHistoryPropagation_SetsPropagationScope_OwnHistory()
    {
        var options = new ChildWorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.OwnHistory);
        Assert.Equal(HistoryPropagationScope.OwnHistory, options.PropagationScope);
    }

    [Fact]
    public void WithHistoryPropagation_SetsPropagationScope_Lineage()
    {
        var options = new ChildWorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.Lineage);
        Assert.Equal(HistoryPropagationScope.Lineage, options.PropagationScope);
    }

    [Fact]
    public void WithHistoryPropagation_DoesNotMutateOriginalOptions()
    {
        var original = new ChildWorkflowTaskOptions(InstanceId: "id-1");
        var updated = original.WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

        Assert.Equal(HistoryPropagationScope.None, original.PropagationScope);
        Assert.Equal(HistoryPropagationScope.OwnHistory, updated.PropagationScope);
        Assert.Equal("id-1", updated.InstanceId);
    }

    // ── Schedule-side: propagation scope set on CreateSubOrchestrationAction ──

    [Fact]
    public async Task CallChildWorkflowAsync_WithNone_DoesNotSetPropagationScope()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        var childTask = context.CallChildWorkflowAsync<int>(
            "ChildWf",
            options: new ChildWorkflowTaskOptions(PropagationScope: HistoryPropagationScope.None));

        // Complete the child synchronously via history
        context.ProcessEvents([
            new HistoryEvent { EventId = 0, SubOrchestrationInstanceCreated = new SubOrchestrationInstanceCreatedEvent { Name = "ChildWf" } },
            new HistoryEvent { SubOrchestrationInstanceCompleted = new SubOrchestrationInstanceCompletedEvent { TaskScheduledId = 0, Result = "99" } }
        ], isReplaying: false);

        var action = context.PendingActions.OfType<OrchestratorAction>()
            .Select(a => a.CreateSubOrchestration)
            .FirstOrDefault(a => a is not null);

        // None: action either absent (cleared after history) or scope is None/unset
        // The create action is removed from pending after history match
        Assert.Empty(context.PendingActions);
        Assert.Equal(99, await childTask);
    }

    [Fact]
    public void CallChildWorkflowAsync_WithOwnHistory_SetsPropagationScopeOnAction()
    {
        var ownHistory = new List<HistoryEvent>
        {
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "TestWorkflow" }),
            MakeEvent(2, e => e.TaskScheduled = new TaskScheduledEvent { Name = "SomeActivity" }),
        };

        var context = CreateContext(instanceId: "parent", appId: "my-app", ownHistory: ownHistory);
        _ = context.CallChildWorkflowAsync<string>(
            "ChildWf",
            options: new ChildWorkflowTaskOptions(PropagationScope: HistoryPropagationScope.OwnHistory));

        var action = context.PendingActions
            .Select(a => a.CreateSubOrchestration)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.OwnHistory, action.HistoryPropagationScope);
        Assert.Single(action.PropagatedHistory);
        Assert.Equal("parent", action.PropagatedHistory[0].InstanceId);
        Assert.Equal("my-app", action.PropagatedHistory[0].AppId);
        Assert.Equal("TestWorkflow", action.PropagatedHistory[0].WorkflowName);
        Assert.Equal(2, action.PropagatedHistory[0].Events.Count);
    }

    [Fact]
    public void CallChildWorkflowAsync_WithLineage_IncludesOwnAndAncestorHistory()
    {
        var grandparentSegment = new PropagatedHistorySegment
        {
            AppId = "grandparent-app",
            InstanceId = "grandparent-inst",
            WorkflowName = "GrandparentWf"
        };

        var ownHistory = new List<HistoryEvent>
        {
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "ParentWf" })
        };

        var context = CreateContext(
            name: "ParentWf",
            instanceId: "parent-inst",
            appId: "parent-app",
            ownHistory: ownHistory,
            incomingPropagatedHistory: [grandparentSegment]);

        _ = context.CallChildWorkflowAsync<string>(
            "ChildWf",
            options: new ChildWorkflowTaskOptions(PropagationScope: HistoryPropagationScope.Lineage));

        var action = context.PendingActions
            .Select(a => a.CreateSubOrchestration)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.Lineage, action.HistoryPropagationScope);
        // Own history segment + grandparent segment
        Assert.Equal(2, action.PropagatedHistory.Count);

        var ownSeg = action.PropagatedHistory.First(s => s.InstanceId == "parent-inst");
        Assert.Equal("parent-app", ownSeg.AppId);
        Assert.Equal("ParentWf", ownSeg.WorkflowName);
        Assert.Single(ownSeg.Events);

        var ancestorSeg = action.PropagatedHistory.First(s => s.InstanceId == "grandparent-inst");
        Assert.Equal("grandparent-app", ancestorSeg.AppId);
        Assert.Equal("GrandparentWf", ancestorSeg.WorkflowName);
    }

    [Fact]
    public void CallChildWorkflowAsync_WithOwnHistory_NoLineage_ExcludesAncestors()
    {
        var grandparentSegment = new PropagatedHistorySegment
        {
            AppId = "gp-app", InstanceId = "gp-inst", WorkflowName = "GpWf"
        };

        var context = CreateContext(
            name: "ParentWf",
            instanceId: "parent-inst",
            appId: "parent-app",
            incomingPropagatedHistory: [grandparentSegment]);

        _ = context.CallChildWorkflowAsync<string>(
            "ChildWf",
            options: new ChildWorkflowTaskOptions(PropagationScope: HistoryPropagationScope.OwnHistory));

        var action = context.PendingActions
            .Select(a => a.CreateSubOrchestration)
            .First(a => a is not null);

        // Only own history, NOT grandparent
        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.OwnHistory, action.HistoryPropagationScope);
        Assert.Single(action.PropagatedHistory);
        Assert.Equal("parent-inst", action.PropagatedHistory[0].InstanceId);
    }

    // ── PropagatedHistory constructor validation ──────────────────────────────

    [Fact]
    public void PropagatedHistory_Constructor_ThrowsOnNullEntries()
    {
        Assert.Throws<ArgumentNullException>(() => new PropagatedHistory(null!));
    }

    [Fact]
    public void PropagatedHistory_Entries_ReflectsConstructorInput()
    {
        var entries = new[] { new PropagatedHistoryEntry("a", "b", "c", []) };
        var history = new PropagatedHistory(entries);
        Assert.Single(history.Entries);
        Assert.Same(entries[0], history.Entries[0]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HistoryEvent MakeEvent(int id, Action<HistoryEvent> configure)
    {
        var e = new HistoryEvent
        {
            EventId = id,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        configure(e);
        return e;
    }
}
