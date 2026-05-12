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

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Contains the workflow history that was propagated from ancestor workflow instances.
/// Each entry corresponds to a single ancestor's history.
/// </summary>
/// <remarks>
/// A workflow receives propagated history when it is scheduled with a
/// <see cref="HistoryPropagationScope"/> other than <see cref="HistoryPropagationScope.None"/>.
/// Use <see cref="WorkflowContext.GetPropagatedHistory"/> to retrieve the propagated history
/// inside a workflow implementation.
/// </remarks>
public sealed class PropagatedHistory
{
    private readonly IReadOnlyList<PropagatedHistoryEntry> _entries;

    /// <summary>
    /// Initializes a new instance of <see cref="PropagatedHistory"/> with the given entries.
    /// </summary>
    /// <param name="entries">The propagated history entries from ancestor workflows.</param>
    public PropagatedHistory(IReadOnlyList<PropagatedHistoryEntry> entries)
    {
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    /// <summary>
    /// Gets the ordered list of propagated history entries.
    /// The first entry corresponds to the immediate parent workflow; subsequent entries
    /// correspond to progressively older ancestors when <see cref="HistoryPropagationScope.Lineage"/> is used.
    /// </summary>
    public IReadOnlyList<PropagatedHistoryEntry> Entries => _entries;

    /// <summary>
    /// Returns a new <see cref="PropagatedHistory"/> containing only entries from the specified App ID.
    /// </summary>
    /// <param name="appId">The Dapr App ID to filter by.</param>
    /// <returns>A filtered <see cref="PropagatedHistory"/> instance.</returns>
    public PropagatedHistory FilterByAppId(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return new PropagatedHistory(
            _entries.Where(e => string.Equals(e.AppId, appId, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    /// <summary>
    /// Returns a new <see cref="PropagatedHistory"/> containing only the entry with the specified instance ID.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID to filter by.</param>
    /// <returns>A filtered <see cref="PropagatedHistory"/> instance.</returns>
    public PropagatedHistory FilterByInstanceId(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return new PropagatedHistory(
            _entries.Where(e => string.Equals(e.InstanceId, instanceId, StringComparison.Ordinal)).ToList());
    }

    /// <summary>
    /// Returns a new <see cref="PropagatedHistory"/> containing only entries for the specified workflow name.
    /// </summary>
    /// <param name="workflowName">The workflow name to filter by.</param>
    /// <returns>A filtered <see cref="PropagatedHistory"/> instance.</returns>
    public PropagatedHistory FilterByWorkflowName(string workflowName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        return new PropagatedHistory(
            _entries.Where(e => string.Equals(e.WorkflowName, workflowName, StringComparison.Ordinal)).ToList());
    }
}
