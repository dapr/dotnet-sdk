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
/// A single workflow's contribution to a propagated history: the ancestor workflow's identity,
/// plus the activities and child workflows it executed.
/// </summary>
/// <param name="instanceId">The instance ID of the ancestor workflow.</param>
/// <param name="appId">The Dapr App ID that ran the ancestor workflow.</param>
/// <param name="name">The name of the ancestor workflow.</param>
/// <param name="activities">Activities resolved from this entry, in execution order.</param>
/// <param name="childWorkflows">Child workflows resolved from this entry, in execution order.</param>
/// <remarks>
/// One <see cref="PropagatedHistoryEntry"/> exists per ancestor workflow in a
/// <see cref="PropagatedHistory"/>. Use <see cref="TryGetLastActivityByName"/> and
/// <see cref="TryGetLastChildWorkflowByName"/> to look up specific items in this entry;
/// the plural <c>Get*ByName</c> variants return every occurrence in execution order.
/// </remarks>
public sealed class PropagatedHistoryEntry(
    string instanceId,
    string appId,
    string name,
    IReadOnlyList<PropagatedHistoryActivityResult> activities,
    IReadOnlyList<PropagatedHistoryChildWorkflowResult> childWorkflows)
{
    private readonly IReadOnlyList<PropagatedHistoryActivityResult> _activities =
        activities ?? throw new ArgumentNullException(nameof(activities));
    private readonly IReadOnlyList<PropagatedHistoryChildWorkflowResult> _childWorkflows =
        childWorkflows ?? throw new ArgumentNullException(nameof(childWorkflows));

    /// <summary>The instance ID of the ancestor workflow this entry describes.</summary>
    public string InstanceId { get; } = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

    /// <summary>The Dapr App ID that ran this ancestor workflow.</summary>
    public string AppId { get; } = appId ?? throw new ArgumentNullException(nameof(appId));

    /// <summary>The name of this ancestor workflow.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>All activities executed in this entry, in execution order.</summary>
    public IReadOnlyList<PropagatedHistoryActivityResult> Activities => _activities;

    /// <summary>All child workflows started in this entry, in execution order.</summary>
    public IReadOnlyList<PropagatedHistoryChildWorkflowResult> ChildWorkflows => _childWorkflows;

    /// <summary>
    /// Returns every activity in this entry whose scheduled name matches, in execution order.
    /// </summary>
    /// <param name="name">The activity name to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<PropagatedHistoryActivityResult> GetActivitiesByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _activities
            .Where(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Tries to return the most recent activity in this entry whose name matches.
    /// </summary>
    /// <param name="name">The activity name to look up.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the last matching activity; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a matching activity was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetLastActivityByName(string name, [NotNullWhen(true)] out PropagatedHistoryActivityResult? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        for (var i = _activities.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_activities[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                result = _activities[i];
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Returns every child workflow in this entry whose name matches, in execution order.
    /// </summary>
    /// <param name="name">The child workflow name to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<PropagatedHistoryChildWorkflowResult> GetChildWorkflowsByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _childWorkflows
            .Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Tries to return the most recent child workflow in this entry whose name matches.
    /// </summary>
    /// <param name="name">The child workflow name to look up.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the last matching child workflow; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a matching child workflow was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetLastChildWorkflowByName(string name, [NotNullWhen(true)] out PropagatedHistoryChildWorkflowResult? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        for (var i = _childWorkflows.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_childWorkflows[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                result = _childWorkflows[i];
                return true;
            }
        }

        result = null;
        return false;
    }
}
