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

namespace Dapr.Workflow;

/// <summary>
/// Identifies the kind of a workflow history event returned in propagated history.
/// </summary>
public enum HistoryEventKind
{
    /// <summary>
    /// Unknown or unsupported event type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The workflow execution started.
    /// </summary>
    ExecutionStarted,

    /// <summary>
    /// The workflow execution completed.
    /// </summary>
    ExecutionCompleted,

    /// <summary>
    /// The workflow execution was terminated.
    /// </summary>
    ExecutionTerminated,

    /// <summary>
    /// An activity task was scheduled.
    /// </summary>
    TaskScheduled,

    /// <summary>
    /// An activity task completed successfully.
    /// </summary>
    TaskCompleted,

    /// <summary>
    /// An activity task failed.
    /// </summary>
    TaskFailed,

    /// <summary>
    /// A child workflow instance was created.
    /// </summary>
    SubOrchestrationInstanceCreated,

    /// <summary>
    /// A child workflow instance completed successfully.
    /// </summary>
    SubOrchestrationInstanceCompleted,

    /// <summary>
    /// A child workflow instance failed.
    /// </summary>
    SubOrchestrationInstanceFailed,

    /// <summary>
    /// A durable timer was created.
    /// </summary>
    TimerCreated,

    /// <summary>
    /// A durable timer fired.
    /// </summary>
    TimerFired,

    /// <summary>
    /// The orchestrator started a processing turn.
    /// </summary>
    OrchestratorStarted,

    /// <summary>
    /// The orchestrator completed a processing turn.
    /// </summary>
    OrchestratorCompleted,

    /// <summary>
    /// An event was sent to another workflow instance.
    /// </summary>
    EventSent,

    /// <summary>
    /// An external event was raised for this workflow instance.
    /// </summary>
    EventRaised,

    /// <summary>
    /// The workflow continued as new.
    /// </summary>
    ContinueAsNew,

    /// <summary>
    /// The workflow execution was suspended.
    /// </summary>
    ExecutionSuspended,

    /// <summary>
    /// The workflow execution was resumed.
    /// </summary>
    ExecutionResumed
}
