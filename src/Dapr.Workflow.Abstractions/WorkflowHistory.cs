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
/// Scheduling-side helpers for workflow history propagation, mirroring the
/// <c>workflow.PropagateLineage()</c> / <c>workflow.PropagateOwnHistory()</c>
/// factories in the Go SDK.
/// </summary>
/// <remarks>
/// Both forms are equivalent: <c>options.WithHistoryPropagation(WorkflowHistory.PropagateLineage())</c>
/// and <c>options.WithHistoryPropagation(HistoryPropagationScope.Lineage)</c> produce the same scope.
/// The factory helpers exist for cross-SDK call-site parity.
/// </remarks>
public static class WorkflowHistory
{
    /// <summary>
    /// Returns the <see cref="HistoryPropagationScope"/> that propagates the caller's own
    /// events plus any ancestor events it received.
    /// </summary>
    /// <remarks>
    /// Use for chain-of-custody verification where downstream code needs visibility into
    /// the full lineage of upstream workflows.
    /// </remarks>
    public static HistoryPropagationScope PropagateLineage() => HistoryPropagationScope.Lineage;

    /// <summary>
    /// Returns the <see cref="HistoryPropagationScope"/> that propagates the caller's events
    /// only; ancestor history is dropped.
    /// </summary>
    /// <remarks>
    /// Use as a trust boundary, where downstream code should only see the immediate caller.
    /// </remarks>
    public static HistoryPropagationScope PropagateOwnHistory() => HistoryPropagationScope.OwnHistory;
}
