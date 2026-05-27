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
/// A reconstructed view of a single child workflow invocation surfaced through propagated workflow history.
/// </summary>
/// <param name="Name">The scheduled name of the child workflow.</param>
/// <param name="Started">Whether the child workflow was scheduled in the propagated history.</param>
/// <param name="Completed">Whether the child workflow completed successfully.</param>
/// <param name="Failed">Whether the child workflow failed.</param>
/// <param name="Output">The JSON-encoded output payload, or <c>null</c> when the child workflow has not completed.</param>
/// <param name="FailureDetails">The failure details when <paramref name="Failed"/> is true, otherwise <c>null</c>.</param>
/// <remarks>
/// Mirrors the <c>ChildWorkflowResult</c> type in the Go and Python SDKs. The
/// <see cref="Started"/> / <see cref="Completed"/> / <see cref="Failed"/> flags carry that
/// parity; <see cref="Status"/> projects them onto a single value for callers that prefer to
/// <c>switch</c> on the lifecycle.
/// </remarks>
public sealed record PropagatedHistoryChildWorkflowResult(
    string Name,
    bool Started,
    bool Completed,
    bool Failed,
    string? Output,
    WorkflowTaskFailureDetails? FailureDetails)
{
    /// <summary>
    /// The resolved lifecycle status of this child workflow, derived from the
    /// <see cref="Completed"/> and <see cref="Failed"/> flags. <see cref="Failed"/> takes
    /// precedence over <see cref="Completed"/>.
    /// </summary>
    public PropagatedHistoryTaskStatus Status =>
        Failed ? PropagatedHistoryTaskStatus.Failed
        : Completed ? PropagatedHistoryTaskStatus.Completed
        : PropagatedHistoryTaskStatus.Pending;
}
