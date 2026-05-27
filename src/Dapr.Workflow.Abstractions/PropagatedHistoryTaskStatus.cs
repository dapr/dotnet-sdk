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
/// The resolved lifecycle status of a task (activity or child workflow) reconstructed from
/// propagated workflow history.
/// </summary>
/// <remarks>
/// Every task surfaced through propagated history was scheduled, so the status reflects how
/// far it progressed past scheduling. It is a projection of the <c>Completed</c> and
/// <c>Failed</c> flags on <see cref="PropagatedHistoryActivityResult"/> /
/// <see cref="PropagatedHistoryChildWorkflowResult"/>, provided so callers can <c>switch</c>
/// on a single value instead of evaluating the flags by hand.
/// </remarks>
public enum PropagatedHistoryTaskStatus
{
    /// <summary>
    /// The task was scheduled but has not yet completed or failed in the propagated history.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The task completed successfully.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The task failed.
    /// </summary>
    Failed = 2,
}
