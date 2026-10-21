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

using Dapr.Workflow.Abstractions;

namespace Dapr.Workflow;

/// <summary>
/// Defines properties and methods for task activity context objects.
/// </summary>
public abstract class WorkflowActivityContext
{
    /// <summary>
    /// Gets the name of the activity.
    /// </summary>
    public abstract TaskIdentifier Identifier { get; }
    
    /// <summary>
    /// Gets the task execution key.
    /// </summary>
    public abstract string TaskExecutionKey { get; }

    /// <summary>
    /// Gets the ID of the TaskScheduled history event for this activity attempt.
    /// </summary>
    /// <remarks>
    /// This value is supplied by the runtime in the activity request's TaskId field.
    /// It is distinct from TaskExecutionKey and is available before the activity completes.
    /// Use it with the source instance ID when requesting a workflow rerun.
    /// </remarks>
    public virtual int ScheduledEventId => throw new System.NotSupportedException(
        "This activity context does not expose the scheduling history event ID.");

    /// <summary>
    /// Gets the unique ID of the current workflow instance.
    /// </summary>
    public abstract string InstanceId {  get; }
}
