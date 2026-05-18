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
/// a propagation scope on <see cref="ChildWorkflowTaskOptions"/>, propagating
/// the scope to the outgoing <see cref="CreateChildWorkflowAction"/>, and
/// exposing inbound propagated history through <see cref="WorkflowContext.GetPropagatedHistory"/>.
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
            Timestamp = Timestamp.FromDateTime(timestamp ?? StartTime)
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

    // ------------------------------------------------------------------
    //  GetPropagatedHistory
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
    public void GetPropagatedHistory_ReturnsSingleEntry_WhenOneChunkPropagated()
    {
        var chunk = MakeChunk("parent-app", "parent-instance", "ParentWorkflow",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "ParentWorkflow" }));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);

        var history = context.GetPropagatedHistory();

        Assert.NotNull(history);
        Assert.Single(history.Entries);
        Assert.Equal("parent-app", history.Entries[0].AppId);
        Assert.Equal("parent-instance", history.Entries[0].InstanceId);
        Assert.Equal("ParentWorkflow", history.Entries[0].WorkflowName);
        Assert.Single(history.Entries[0].Events);
        Assert.Equal(HistoryEventKind.ExecutionStarted, history.Entries[0].Events[0].Kind);
    }

    [Fact]
    public void GetPropagatedHistory_PreservesChunkOrder()
    {
        var parent = MakeChunk("p-app", "p-inst", "ParentWf",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "ParentWf" }));
        var grandparent = MakeChunk("gp-app", "gp-inst", "GrandparentWf",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent { Name = "GrandparentWf" }));

        var context = CreateContext(incomingPropagatedHistory: [parent, grandparent]);
        var history = context.GetPropagatedHistory();

        Assert.NotNull(history);
        Assert.Equal(2, history.Entries.Count);
        Assert.Equal("p-inst", history.Entries[0].InstanceId);
        Assert.Equal("gp-inst", history.Entries[1].InstanceId);
    }

    [Fact]
    public void GetPropagatedHistory_MapsAllHistoryEventKinds()
    {
        var mappings = new Dictionary<HistoryEvent, HistoryEventKind>
        {
            { MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent()), HistoryEventKind.ExecutionStarted },
            { MakeEvent(2, e => e.ExecutionCompleted = new ExecutionCompletedEvent()), HistoryEventKind.ExecutionCompleted },
            { MakeEvent(3, e => e.ExecutionTerminated = new ExecutionTerminatedEvent()), HistoryEventKind.ExecutionTerminated },
            { MakeEvent(4, e => e.TaskScheduled = new TaskScheduledEvent { Name = "a" }), HistoryEventKind.TaskScheduled },
            { MakeEvent(5, e => e.TaskCompleted = new TaskCompletedEvent()), HistoryEventKind.TaskCompleted },
            { MakeEvent(6, e => e.TaskFailed = new TaskFailedEvent()), HistoryEventKind.TaskFailed },
            { MakeEvent(7, e => e.ChildWorkflowInstanceCreated = new ChildWorkflowInstanceCreatedEvent()), HistoryEventKind.SubOrchestrationInstanceCreated },
            { MakeEvent(8, e => e.ChildWorkflowInstanceCompleted = new ChildWorkflowInstanceCompletedEvent()), HistoryEventKind.SubOrchestrationInstanceCompleted },
            { MakeEvent(9, e => e.ChildWorkflowInstanceFailed = new ChildWorkflowInstanceFailedEvent()), HistoryEventKind.SubOrchestrationInstanceFailed },
            { MakeEvent(10, e => e.TimerCreated = new TimerCreatedEvent()), HistoryEventKind.TimerCreated },
            { MakeEvent(11, e => e.TimerFired = new TimerFiredEvent()), HistoryEventKind.TimerFired },
            { MakeEvent(12, e => e.WorkflowStarted = new WorkflowStartedEvent()), HistoryEventKind.OrchestratorStarted },
            { MakeEvent(13, e => e.WorkflowCompleted = new WorkflowCompletedEvent()), HistoryEventKind.OrchestratorCompleted },
            { MakeEvent(14, e => e.EventSent = new EventSentEvent()), HistoryEventKind.EventSent },
            { MakeEvent(15, e => e.EventRaised = new EventRaisedEvent()), HistoryEventKind.EventRaised },
            { MakeEvent(16, e => e.ContinueAsNew = new ContinueAsNewEvent()), HistoryEventKind.ContinueAsNew },
            { MakeEvent(17, e => e.ExecutionSuspended = new ExecutionSuspendedEvent()), HistoryEventKind.ExecutionSuspended },
            { MakeEvent(18, e => e.ExecutionResumed = new ExecutionResumedEvent()), HistoryEventKind.ExecutionResumed },
        };

        var chunk = MakeChunk("app", "inst", "Wf", mappings.Keys.ToArray());
        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        var entry = context.GetPropagatedHistory()!.Entries.Single();

        Assert.Equal(mappings.Count, entry.Events.Count);
        var expectedKinds = mappings.Values.ToList();
        for (var i = 0; i < entry.Events.Count; i++)
        {
            Assert.Equal(expectedKinds[i], entry.Events[i].Kind);
        }
    }

    [Fact]
    public void GetPropagatedHistory_MapsUnsetEventTypeToUnknown()
    {
        // An event with no oneof case set should be mapped to Unknown rather than crashing.
        var bareEvent = new HistoryEvent { EventId = 42, Timestamp = Timestamp.FromDateTime(StartTime) };
        var chunk = MakeChunk("app", "inst", "Wf", bareEvent);

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        var entry = context.GetPropagatedHistory()!.Entries.Single();

        Assert.Single(entry.Events);
        Assert.Equal(HistoryEventKind.Unknown, entry.Events[0].Kind);
        Assert.Equal(42, entry.Events[0].EventId);
    }

    [Fact]
    public void GetPropagatedHistory_SkipsMalformedRawEvents()
    {
        var chunk = new PropagatedHistoryChunk
        {
            AppId = "app",
            InstanceId = "inst",
            WorkflowName = "Wf",
        };
        // Add a malformed event (not a valid serialized HistoryEvent).
        chunk.RawEvents.Add(ByteString.CopyFrom(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff }));
        // Followed by a well-formed event.
        chunk.RawEvents.Add(MakeEvent(7, e => e.TaskCompleted = new TaskCompletedEvent()).ToByteString());

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        var entry = context.GetPropagatedHistory()!.Entries.Single();

        // Only the well-formed event survives.
        Assert.Single(entry.Events);
        Assert.Equal(7, entry.Events[0].EventId);
        Assert.Equal(HistoryEventKind.TaskCompleted, entry.Events[0].Kind);
    }

    [Fact]
    public void GetPropagatedHistory_PreservesEventTimestamp()
    {
        var when = new DateTime(2026, 06, 15, 12, 30, 45, DateTimeKind.Utc);
        var chunk = MakeChunk("app", "inst", "Wf",
            MakeEvent(1, e => e.ExecutionStarted = new ExecutionStartedEvent(), timestamp: when));

        var context = CreateContext(incomingPropagatedHistory: [chunk]);
        var entry = context.GetPropagatedHistory()!.Entries.Single();

        Assert.Equal(when, entry.Events[0].Timestamp.UtcDateTime);
    }

    // ------------------------------------------------------------------
    //  PropagatedHistory filters
    // ------------------------------------------------------------------

    [Fact]
    public void FilterByAppId_ReturnsOnlyMatchingEntries_CaseInsensitive()
    {
        var history = new PropagatedHistory(new[]
        {
            new PropagatedHistoryEntry("app-a", "i1", "WfA", []),
            new PropagatedHistoryEntry("app-b", "i2", "WfB", []),
            new PropagatedHistoryEntry("APP-A", "i3", "WfA2", []),
        });

        var filtered = history.FilterByAppId("app-a");

        Assert.Equal(2, filtered.Entries.Count);
        Assert.All(filtered.Entries, e => Assert.Equal("app-a", e.AppId, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void FilterByInstanceId_ReturnsOnlyMatchingEntry_CaseSensitive()
    {
        var history = new PropagatedHistory(new[]
        {
            new PropagatedHistoryEntry("app", "Instance-1", "Wf", []),
            new PropagatedHistoryEntry("app", "instance-2", "Wf", []),
        });

        Assert.Single(history.FilterByInstanceId("Instance-1").Entries);
        Assert.Empty(history.FilterByInstanceId("instance-1").Entries);
    }

    [Fact]
    public void FilterByWorkflowName_ReturnsOnlyMatchingEntries_CaseSensitive()
    {
        var history = new PropagatedHistory(new[]
        {
            new PropagatedHistoryEntry("app", "i1", "PaymentWorkflow", []),
            new PropagatedHistoryEntry("app", "i2", "OrderWorkflow", []),
            new PropagatedHistoryEntry("app", "i3", "PaymentWorkflow", []),
            new PropagatedHistoryEntry("app", "i4", "paymentworkflow", []),
        });

        var filtered = history.FilterByWorkflowName("PaymentWorkflow");

        Assert.Equal(2, filtered.Entries.Count);
        Assert.All(filtered.Entries, e => Assert.Equal("PaymentWorkflow", e.WorkflowName));
    }

    [Fact]
    public void Filters_ThrowOnEmptyOrWhitespace()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentException>(() => history.FilterByAppId(string.Empty));
        Assert.Throws<ArgumentException>(() => history.FilterByInstanceId("   "));
        Assert.Throws<ArgumentException>(() => history.FilterByWorkflowName(string.Empty));
    }

    [Fact]
    public void PropagatedHistory_Ctor_ThrowsOnNullEntries()
    {
        Assert.Throws<ArgumentNullException>(() => new PropagatedHistory(null!));
    }

    // ------------------------------------------------------------------
    //  ChildWorkflowTaskOptions.WithHistoryPropagation
    // ------------------------------------------------------------------

    [Fact]
    public void WithHistoryPropagation_SetsScope()
    {
        var options = new ChildWorkflowTaskOptions().WithHistoryPropagation(HistoryPropagationScope.Lineage);
        Assert.Equal(HistoryPropagationScope.Lineage, options.PropagationScope);
    }

    [Fact]
    public void WithHistoryPropagation_DoesNotMutateOriginal()
    {
        var original = new ChildWorkflowTaskOptions(InstanceId: "id-1");
        var updated = original.WithHistoryPropagation(HistoryPropagationScope.OwnHistory);

        Assert.Equal(HistoryPropagationScope.None, original.PropagationScope);
        Assert.Equal(HistoryPropagationScope.OwnHistory, updated.PropagationScope);
        Assert.Equal("id-1", updated.InstanceId);
    }

    // ------------------------------------------------------------------
    //  CallChildWorkflowAsync — outbound HistoryPropagationScope on action
    // ------------------------------------------------------------------

    [Fact]
    public void CallChildWorkflowAsync_DefaultScope_LeavesActionScopeUnset()
    {
        var context = CreateContext(instanceId: "parent", appId: "my-app");
        _ = context.CallChildWorkflowAsync<string>("ChildWf");

        var action = context.PendingActions
            .Select(a => a.CreateChildWorkflow)
            .First(a => a is not null);

        // Default = None => HistoryPropagationScope field is left at its proto default (None / unset).
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
            options: new ChildWorkflowTaskOptions(PropagationScope: HistoryPropagationScope.Lineage));

        var action = context.PendingActions
            .Select(a => a.CreateChildWorkflow)
            .First(a => a is not null);

        Assert.Equal(Dapr.DurableTask.Protobuf.HistoryPropagationScope.Lineage, action.HistoryPropagationScope);
    }
}
