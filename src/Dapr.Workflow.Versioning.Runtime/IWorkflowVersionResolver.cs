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
/// Resolves the latest <see cref="WorkflowVersionIdentity"/> for a given <see cref="WorkflowFamily"/> using the
/// active strategy and selector.
/// </summary>
public interface IWorkflowVersionResolver
{
    /// <summary>
    /// Attempts to select the latest version for the provided family.
    /// </summary>
    /// <param name="family">The workflow family (canonical name and version candidates).</param>
    /// <param name="latest">On success, receives the selected latest identity.</param>
    /// <param name="diagnosticId">On failure, receives a stable diagnostic ID.</param>
    /// <param name="diagnosticMessage">On failure, receives a human-readable message.</param>
    /// <returns><see langword="true"/> if selection succeeded; otherwise <see langword="false"/>.</returns>
    bool TryGetLatest(WorkflowFamily family, out WorkflowVersionIdentity latest, out string? diagnosticId,
        out string? diagnosticMessage);
}
