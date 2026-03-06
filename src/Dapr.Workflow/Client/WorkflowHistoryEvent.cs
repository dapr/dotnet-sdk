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

namespace Dapr.Workflow.Client;

/// <summary>
/// Represents a single event in a workflow instance's history.
/// </summary>
/// <param name="EventId">The unique event ID within the workflow instance history.</param>
/// <param name="EventType">The type of history event.</param>
/// <param name="Timestamp">The timestamp when the event occurred.</param>
public sealed record WorkflowHistoryEvent(
    int EventId,
    WorkflowHistoryEventType EventType,
    DateTime Timestamp);

/// <summary>
/// Represents the type of a workflow history event.
/// </summary>
public enum WorkflowHistoryEventType
{
    /// <summary>Unknown event type.</summary>
    Unknown = 0,

    /// <summary>The workflow execution started.</summary>
    ExecutionStarted,

    /// <summary>The workflow execution completed.</summary>
    ExecutionCompleted,

    /// <summary>The workflow execution was terminated.</summary>
    ExecutionTerminated,

    /// <summary>A task (activity) was scheduled.</summary>
    TaskScheduled,

    /// <summary>A task (activity) completed successfully.</summary>
    TaskCompleted,

    /// <summary>A task (activity) failed.</summary>
    TaskFailed,

    /// <summary>A sub-orchestration instance was created.</summary>
    SubOrchestrationInstanceCreated,

    /// <summary>A sub-orchestration instance completed.</summary>
    SubOrchestrationInstanceCompleted,

    /// <summary>A sub-orchestration instance failed.</summary>
    SubOrchestrationInstanceFailed,

    /// <summary>A timer was created.</summary>
    TimerCreated,

    /// <summary>A timer fired.</summary>
    TimerFired,

    /// <summary>An orchestrator started processing.</summary>
    OrchestratorStarted,

    /// <summary>An orchestrator completed processing.</summary>
    OrchestratorCompleted,

    /// <summary>An event was sent to another instance.</summary>
    EventSent,

    /// <summary>An external event was raised.</summary>
    EventRaised,

    /// <summary>A generic event.</summary>
    GenericEvent,

    /// <summary>A history state event.</summary>
    HistoryState,

    /// <summary>The workflow continued as new.</summary>
    ContinueAsNew,

    /// <summary>The workflow execution was suspended.</summary>
    ExecutionSuspended,

    /// <summary>The workflow execution was resumed.</summary>
    ExecutionResumed,

    /// <summary>The workflow execution stalled.</summary>
    ExecutionStalled,
}
