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
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Serialization;

namespace Dapr.Workflow.Client;

/// <summary>
/// Converts between proto messages and domain models.
/// </summary>
internal static class ProtoConverters
{
    /// <summary>
    /// Converts proto <see cref="OrchestrationState"/> to <see cref="WorkflowMetadata"/>.
    /// </summary>
    public static WorkflowMetadata ToWorkflowMetadata(OrchestrationState state, IWorkflowSerializer serializer) =>
        new(state.InstanceId, state.Name, ToRuntimeStatus(state.OrchestrationStatus),
            state.CreatedTimestamp?.ToDateTime() ?? DateTime.MinValue,
            state.LastUpdatedTimestamp?.ToDateTime() ?? DateTime.MinValue, serializer)
        {
            SerializedInput = string.IsNullOrEmpty(state.Input) ? null : state.Input,
            SerializedOutput = string.IsNullOrEmpty(state.Output) ? null : state.Output,
            SerializedCustomStatus = string.IsNullOrEmpty(state.CustomStatus) ? null : state.CustomStatus
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
}
