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
using System.Diagnostics.CodeAnalysis;
using System.Linq;

/// <summary>
/// Workflow history propagated from one or more ancestor workflows to a child workflow or activity.
/// </summary>
/// <remarks>
/// A propagated history is an ordered list of <see cref="PropagatedHistoryEntry"/> values,
/// one per ancestor workflow. Order is execution order: index 0 is the oldest ancestor,
/// the last entry is the immediate parent.
/// <para>
/// Use the <c>Get*</c> / <c>TryGet*</c> methods to walk the list by app, instance, or workflow name.
/// Mirrors the <c>PropagatedHistory</c> type in the Go and Python SDKs.
/// </para>
/// </remarks>
public sealed class PropagatedHistory
{
    private readonly IReadOnlyList<PropagatedHistoryEntry> _workflows;

    /// <summary>
    /// Initializes a new <see cref="PropagatedHistory"/> from the given workflow entries.
    /// </summary>
    /// <param name="workflows">
    /// Workflow entries in execution order (ancestor first, immediate parent last).
    /// </param>
    public PropagatedHistory(IReadOnlyList<PropagatedHistoryEntry> workflows)
    {
        _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
    }

    /// <summary>
    /// Returns every workflow entry in the propagated history, in execution order
    /// (ancestor first, immediate parent last).
    /// </summary>
    public IReadOnlyList<PropagatedHistoryEntry> GetWorkflows() => _workflows;

    /// <summary>
    /// Returns an ordered, deduplicated list of app IDs in this propagated history.
    /// </summary>
    public IReadOnlyList<string> GetAppIds()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(_workflows.Count);
        foreach (var workflow in _workflows)
        {
            if (seen.Add(workflow.AppId))
            {
                result.Add(workflow.AppId);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns every workflow entry whose name matches, in execution order. Useful when the
    /// list contains the same name more than once (e.g. recursion or ContinueAsNew).
    /// </summary>
    /// <param name="name">The workflow name to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<PropagatedHistoryEntry> GetWorkflowsByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _workflows
            .Where(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Tries to return the most recent workflow entry whose name matches.
    /// </summary>
    /// <param name="name">The workflow name to look up.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the last matching workflow entry; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a matching entry was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetLastWorkflowByName(string name, [NotNullWhen(true)] out PropagatedHistoryEntry? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        for (var i = _workflows.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_workflows[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                result = _workflows[i];
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Returns every workflow entry produced by the given app, in execution order.
    /// </summary>
    /// <param name="appId">The Dapr App ID to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<PropagatedHistoryEntry> GetWorkflowsByAppId(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return _workflows
            .Where(w => string.Equals(w.AppId, appId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns every workflow entry produced by the given instance, in execution order.
    /// Usually a single entry, except when the same instance reappears via ContinueAsNew.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<PropagatedHistoryEntry> GetWorkflowsByInstanceId(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return _workflows
            .Where(w => string.Equals(w.InstanceId, instanceId, StringComparison.Ordinal))
            .ToList();
    }
}
