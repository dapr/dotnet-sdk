// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Defines the policy that selects the "latest" workflow version from a set of candidates that share the same
/// canonical name.
/// </summary>
/// <remarks>
/// The selector may apply arbitrary rules (e.g., exclude pre-release tags, prefer a specific branch, or implement
/// canary behaviors) on top of the comparison semantics provided by the active strategy.
/// </remarks>
public interface IWorkflowVersionSelector
{
    /// <summary>
    /// Selects the "latest" version identity from a non-empty set of candidates.
    /// </summary>
    /// <param name="canonicalName">The canonical name shared by all <paramref name="candidates"/>.</param>
    /// <param name="candidates">The collection of workflow version identities to select from.</param>
    /// <param name="strategy">The active versioning strategy, used to order version strings or resolve tiebreakers.</param>
    /// <returns>The chosen latest <see cref="WorkflowVersionIdentity"/>.</returns>
    WorkflowVersionIdentity SelectLatest(string canonicalName, IReadOnlyCollection<WorkflowVersionIdentity> candidates,
        IWorkflowVersionStrategy strategy);
}
