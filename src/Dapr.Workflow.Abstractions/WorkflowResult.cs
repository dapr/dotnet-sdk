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
/// A scoped view of a single workflow's chunk in propagated history.
/// </summary>
/// <remarks>
/// Mirrors the <c>WorkflowResult</c> type in the Go SDK. Use
/// <see cref="GetLastActivityByName"/> / <see cref="GetLastChildWorkflowByName"/>
/// to query specific items inside this chunk; the plural <c>Get*sByName</c>
/// variants return every occurrence in execution order.
/// </remarks>
public sealed class WorkflowResult
{
    private readonly IReadOnlyList<ActivityResult> _activities;
    private readonly IReadOnlyList<ChildWorkflowResult> _childWorkflows;

    /// <summary>
    /// Initializes a new <see cref="WorkflowResult"/>.
    /// </summary>
    /// <param name="instanceId">The instance ID of the ancestor workflow.</param>
    /// <param name="appId">The Dapr App ID that ran the ancestor workflow.</param>
    /// <param name="name">The name of the ancestor workflow.</param>
    /// <param name="activities">Activities resolved from this chunk, in execution order.</param>
    /// <param name="childWorkflows">Child workflows resolved from this chunk, in execution order.</param>
    public WorkflowResult(
        string instanceId,
        string appId,
        string name,
        IReadOnlyList<ActivityResult> activities,
        IReadOnlyList<ChildWorkflowResult> childWorkflows)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        AppId = appId ?? throw new ArgumentNullException(nameof(appId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _activities = activities ?? throw new ArgumentNullException(nameof(activities));
        _childWorkflows = childWorkflows ?? throw new ArgumentNullException(nameof(childWorkflows));
    }

    /// <summary>The instance ID of this workflow chunk's ancestor.</summary>
    public string InstanceId { get; }

    /// <summary>The Dapr App ID that ran this workflow chunk.</summary>
    public string AppId { get; }

    /// <summary>The name of this workflow.</summary>
    public string Name { get; }

    /// <summary>All activities executed in this chunk, in execution order.</summary>
    public IReadOnlyList<ActivityResult> Activities => _activities;

    /// <summary>All child workflows started in this chunk, in execution order.</summary>
    public IReadOnlyList<ChildWorkflowResult> ChildWorkflows => _childWorkflows;

    /// <summary>
    /// Returns every activity in this chunk whose scheduled name matches, in execution order.
    /// </summary>
    /// <param name="name">The activity name to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<ActivityResult> GetActivitiesByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _activities
            .Where(a => string.Equals(a.Name, name, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Returns the most recent activity in this chunk whose name matches.
    /// </summary>
    /// <param name="name">The activity name to look up.</param>
    /// <returns>The last matching activity.</returns>
    /// <exception cref="PropagationNotFoundException">No activity with the given name is present in this chunk.</exception>
    public ActivityResult GetLastActivityByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var matches = GetActivitiesByName(name);
        if (matches.Count == 0)
        {
            throw new PropagationNotFoundException(
                $"no activity named '{name}' in propagated history for workflow '{Name}'");
        }

        return matches[^1];
    }

    /// <summary>
    /// Returns every child workflow in this chunk whose name matches, in execution order.
    /// </summary>
    /// <param name="name">The child workflow name to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<ChildWorkflowResult> GetChildWorkflowsByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _childWorkflows
            .Where(c => string.Equals(c.Name, name, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Returns the most recent child workflow in this chunk whose name matches.
    /// </summary>
    /// <param name="name">The child workflow name to look up.</param>
    /// <returns>The last matching child workflow.</returns>
    /// <exception cref="PropagationNotFoundException">No child workflow with the given name is present in this chunk.</exception>
    public ChildWorkflowResult GetLastChildWorkflowByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var matches = GetChildWorkflowsByName(name);
        if (matches.Count == 0)
        {
            throw new PropagationNotFoundException(
                $"no child workflow named '{name}' in propagated history for workflow '{Name}'");
        }

        return matches[^1];
    }
}
