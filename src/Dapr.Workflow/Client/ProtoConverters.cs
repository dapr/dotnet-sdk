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
// ------------------------------------------------------------------------

using System;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;

namespace Dapr.Workflow.Client;

/// <summary>
/// Converts between proto messages and domain models.
/// </summary>
internal static class ProtoConverters
{
    /// <summary>
    /// Converts proto <see cref="WorkflowState"/> to <see cref="WorkflowMetadata"/>.
    /// </summary>
    public static WorkflowMetadata ToWorkflowMetadata(Dapr.DurableTask.Protobuf.WorkflowState state, IDaprSerializer serializer) =>
        new(state.InstanceId, state.Name, ToRuntimeStatus(state.WorkflowStatus),
            state.CreatedTimestamp?.ToDateTime() ?? DateTime.MinValue,
            state.LastUpdatedTimestamp?.ToDateTime() ?? DateTime.MinValue, serializer)
        {
            SerializedInput = string.IsNullOrEmpty(state.Input) ? null : state.Input,
            SerializedOutput = string.IsNullOrEmpty(state.Output) ? null : state.Output,
            SerializedCustomStatus = string.IsNullOrEmpty(state.CustomStatus) ? null : state.CustomStatus,
            FailureDetails = ToWorkflowTaskFailureDetails(state.FailureDetails),
        };

    /// <summary>
    /// Converts the proto runtime status enum to <see cref="WorkflowRuntimeStatus"/>.
    /// </summary>
    public static WorkflowRuntimeStatus ToRuntimeStatus(OrchestrationStatus status)
        => status switch
        {
            OrchestrationStatus.Running => WorkflowRuntimeStatus.Running,
            OrchestrationStatus.Completed => WorkflowRuntimeStatus.Completed,
            OrchestrationStatus.ContinuedAsNew => WorkflowRuntimeStatus.ContinuedAsNew,
            OrchestrationStatus.Failed => WorkflowRuntimeStatus.Failed,
            OrchestrationStatus.Canceled => WorkflowRuntimeStatus.Canceled,
            OrchestrationStatus.Terminated => WorkflowRuntimeStatus.Terminated,
            OrchestrationStatus.Pending => WorkflowRuntimeStatus.Pending,
            OrchestrationStatus.Suspended => WorkflowRuntimeStatus.Suspended,
            OrchestrationStatus.Stalled => WorkflowRuntimeStatus.Stalled,
            _ => WorkflowRuntimeStatus.Unknown
        };

    /// <summary>
    /// Converts a proto <see cref="HistoryEvent"/> to <see cref="WorkflowHistoryEvent"/>.
    /// </summary>
    public static WorkflowHistoryEvent ToWorkflowHistoryEvent(HistoryEvent historyEvent) =>
        new(historyEvent.EventId,
            ToHistoryEventType(historyEvent.EventTypeCase),
            historyEvent.Timestamp?.ToDateTime() ?? DateTime.MinValue);

    /// <summary>
    /// Converts the proto history event type to <see cref="WorkflowHistoryEventType"/>.
    /// </summary>
    public static WorkflowHistoryEventType ToHistoryEventType(HistoryEvent.EventTypeOneofCase eventType)
        => eventType switch
        {
            HistoryEvent.EventTypeOneofCase.ExecutionStarted => WorkflowHistoryEventType.ExecutionStarted,
            HistoryEvent.EventTypeOneofCase.ExecutionCompleted => WorkflowHistoryEventType.ExecutionCompleted,
            HistoryEvent.EventTypeOneofCase.ExecutionTerminated => WorkflowHistoryEventType.ExecutionTerminated,
            HistoryEvent.EventTypeOneofCase.TaskScheduled => WorkflowHistoryEventType.TaskScheduled,
            HistoryEvent.EventTypeOneofCase.TaskCompleted => WorkflowHistoryEventType.TaskCompleted,
            HistoryEvent.EventTypeOneofCase.TaskFailed => WorkflowHistoryEventType.TaskFailed,
            HistoryEvent.EventTypeOneofCase.ChildWorkflowInstanceCreated => WorkflowHistoryEventType.SubOrchestrationInstanceCreated,
            HistoryEvent.EventTypeOneofCase.ChildWorkflowInstanceCompleted => WorkflowHistoryEventType.SubOrchestrationInstanceCompleted,
            HistoryEvent.EventTypeOneofCase.ChildWorkflowInstanceFailed => WorkflowHistoryEventType.SubOrchestrationInstanceFailed,
            HistoryEvent.EventTypeOneofCase.TimerCreated => WorkflowHistoryEventType.TimerCreated,
            HistoryEvent.EventTypeOneofCase.TimerFired => WorkflowHistoryEventType.TimerFired,
            HistoryEvent.EventTypeOneofCase.WorkflowStarted => WorkflowHistoryEventType.OrchestratorStarted,
            HistoryEvent.EventTypeOneofCase.WorkflowCompleted => WorkflowHistoryEventType.OrchestratorCompleted,
            HistoryEvent.EventTypeOneofCase.EventSent => WorkflowHistoryEventType.EventSent,
            HistoryEvent.EventTypeOneofCase.EventRaised => WorkflowHistoryEventType.EventRaised,
            // HistoryEvent.EventTypeOneofCase.GenericEvent => WorkflowHistoryEventType.GenericEvent,
            // HistoryEvent.EventTypeOneofCase.HistoryState => WorkflowHistoryEventType.HistoryState,
            HistoryEvent.EventTypeOneofCase.ContinueAsNew => WorkflowHistoryEventType.ContinueAsNew,
            HistoryEvent.EventTypeOneofCase.ExecutionSuspended => WorkflowHistoryEventType.ExecutionSuspended,
            HistoryEvent.EventTypeOneofCase.ExecutionResumed => WorkflowHistoryEventType.ExecutionResumed,
            HistoryEvent.EventTypeOneofCase.ExecutionStalled => WorkflowHistoryEventType.ExecutionStalled,
            HistoryEvent.EventTypeOneofCase.DetachedWorkflowInstanceCreated => WorkflowHistoryEventType.SubOrchestrationInstanceCreated,
            _ => WorkflowHistoryEventType.Unknown
        };

    private static Workflow.WorkflowTaskFailureDetails? ToWorkflowTaskFailureDetails(TaskFailureDetails? failureDetails)
        => failureDetails is null
            ? null
            : new Workflow.WorkflowTaskFailureDetails(
                failureDetails.ErrorType,
                failureDetails.ErrorMessage,
                string.IsNullOrEmpty(failureDetails.StackTrace) ? null : failureDetails.StackTrace);
}
