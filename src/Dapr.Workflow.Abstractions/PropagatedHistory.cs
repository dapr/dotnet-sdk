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
/// Workflow history propagated from a parent workflow to a child workflow or activity.
/// </summary>
/// <remarks>
/// A propagated history is composed of one or more chunks, each owned by a distinct
/// workflow instance. Chunks preserve execution order: index 0 is the oldest ancestor,
/// the last chunk is the immediate parent.
/// <para>
/// Use the <c>Get*</c> methods to walk the chain by app, instance, or workflow name.
/// Mirrors the <c>PropagatedHistory</c> type in the Go and Python SDKs.
/// </para>
/// </remarks>
public sealed class PropagatedHistory
{
    private readonly IReadOnlyList<WorkflowResult> _workflows;

    /// <summary>
    /// Initializes a new <see cref="PropagatedHistory"/> from the given workflow chunks.
    /// </summary>
    /// <param name="workflows">
    /// Workflow chunks in execution order (ancestor first, immediate parent last).
    /// </param>
    public PropagatedHistory(IReadOnlyList<WorkflowResult> workflows)
    {
        _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
    }

    /// <summary>
    /// Returns every workflow chunk in the chain, in execution order
    /// (ancestor first, immediate parent last).
    /// </summary>
    public IReadOnlyList<WorkflowResult> GetWorkflows() => _workflows;

    /// <summary>
    /// Returns an ordered, deduplicated list of app IDs in the propagated chain.
    /// </summary>
    public IReadOnlyList<string> GetAppIds()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
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
    /// Returns every workflow whose name matches, in execution order. Useful when the
    /// chain contains the same name more than once (e.g. recursion or ContinueAsNew).
    /// </summary>
    /// <param name="name">The workflow name to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<WorkflowResult> GetWorkflowsByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _workflows
            .Where(w => string.Equals(w.Name, name, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Returns the most recent workflow in the chain whose name matches.
    /// </summary>
    /// <param name="name">The workflow name to look up.</param>
    /// <returns>The last matching workflow chunk.</returns>
    /// <exception cref="PropagationNotFoundException">No workflow with the given name is present in the chain.</exception>
    public WorkflowResult GetLastWorkflowByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var matches = GetWorkflowsByName(name);
        if (matches.Count == 0)
        {
            throw new PropagationNotFoundException($"no workflow named '{name}' in propagated history");
        }

        return matches[^1];
    }

    /// <summary>
    /// Returns every workflow chunk produced by the given app, in execution order.
    /// </summary>
    /// <param name="appId">The Dapr App ID to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<WorkflowResult> GetWorkflowsByAppId(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return _workflows
            .Where(w => string.Equals(w.AppId, appId, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Returns every workflow chunk produced by the given instance, in execution order.
    /// Usually a single entry, except when the same instance reappears via ContinueAsNew.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID to filter by.</param>
    /// <returns>An empty list when no match is found.</returns>
    public IReadOnlyList<WorkflowResult> GetWorkflowsByInstanceId(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return _workflows
            .Where(w => string.Equals(w.InstanceId, instanceId, StringComparison.Ordinal))
            .ToList();
    }
}
