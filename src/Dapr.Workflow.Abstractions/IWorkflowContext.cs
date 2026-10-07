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

namespace Dapr.Workflow;

/// <summary>
/// Provides functionality available to orchestration code.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// Gets a value indicating whether the workflow is currently replaying a previous execution.
    /// </summary>
    /// <remarks>
    /// This property is useful when there is logic that needs to run only when *not* replaying. For example,
    /// certain types of application logging may become too noisy when duplicated as part of replay. The
    /// application code could check to see whether the function is being replayed and then issue
    /// the log statements when this value is <c>false</c>.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the workflow is currently replaying a previous execution; otherwise <c>false</c>.
    /// </value>
    bool IsReplaying { get; }

    /// <summary>
    /// Returns true/false according to patch-based versioning semantics.
    /// </summary>
    /// <param name="patchName">Case-sensitive patch name.</param>
    bool IsPatched(string patchName);
    
    /// <summary>
    /// Gets the current workflow time in UTC.
    /// </summary>
    /// <remarks>
    /// The current workflow time is stored in the workflow history and this API will
    /// return the same value each time it is called from a particular point in the workflow's
    /// execution. It is a deterministic, replay-safe replacement for existing .NET APIs for getting
    /// the current time, such as <see cref="DateTime.UtcNow"/> and <see cref="DateTimeOffset.UtcNow"/>
    /// (which should not be used).
    /// </remarks>
    DateTime CurrentUtcDateTime { get; }

    /// <summary>
    /// Schedules a new workflow instance that has no parent-child relationship with the current workflow.
    /// </summary>
    /// <param name="workflowName">The name of the workflow to schedule.</param>
    /// <param name="instanceId">
    /// The unique ID for the new workflow instance. If not specified, a deterministic GUID is generated.
    /// </param>
    /// <param name="input">The serializable input to pass to the new workflow.</param>
    /// <param name="startTime">
    /// The time when the workflow should start. If in the past or <c>null</c>, the workflow starts immediately.
    /// </param>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    Task<string> ScheduleNewDetachedWorkflowAsync(
        string workflowName,
        string? instanceId = null,
        object? input = null,
        DateTimeOffset? startTime = null) =>
        throw new NotSupportedException("Detached workflow scheduling is not supported by this workflow context.");
}
