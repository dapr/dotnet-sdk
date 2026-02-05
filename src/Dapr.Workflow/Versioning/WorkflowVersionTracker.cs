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

using System;
using System.Collections.Generic;
using Dapr.DurableTask.Protobuf;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Tracks workflow patch version information across replays and orchestrator turns.
/// </summary>
internal sealed class WorkflowVersionTracker
{
    /// <summary>
    /// Full patch evaluation sequence as recorded by history (duplicates allowed, ordered).
    /// </summary>
    private readonly List<string> _historyPatchSequence;

    /// <summary>
    /// Patch names recorded in history (duplicates collapsed).
    /// </summary>
    private readonly HashSet<string> _historyPatches;

    /// <summary>
    /// Patches evaluated in the current execution (duplicates allowed, ordered).
    /// </summary>
    private readonly List<string> _patchesThisTurn = [];

    /// <summary>
    /// Per-turn flags.
    /// </summary>
    private bool _includeVersionInNextResponse;

    /// <summary>
    /// Stall state, if any.
    /// </summary>
    public bool IsStalled => this.StalledEvent != null;
    
    public ExecutionStalledEvent? StalledEvent { get; private set; }

    public bool IncludeVersionInNextResponse => _includeVersionInNextResponse;

    /// <summary>
    /// Ordered patches extracted from history (with duplicates preserved).
    /// </summary>
    public IReadOnlyCollection<string> AggregatedPatchesOrdered => _historyPatchSequence;

    private readonly Dictionary<string, bool> _appliedPatches = new(StringComparer.Ordinal);

    public WorkflowVersionTracker(List<HistoryEvent> events)
    {
        _historyPatchSequence = ListAllVersioningPatches(events);
        _historyPatches = new HashSet<string>(_historyPatchSequence, StringComparer.Ordinal);
    }

    public void OnOrchestratorStarted()
    {
        if (this.IsStalled)
            return;

        _appliedPatches.Clear();
        _patchesThisTurn.Clear();
        _includeVersionInNextResponse = false;
    }
    
    /// <summary>
    /// Request enabling/using a patch from workflow code. Returns IsPatched result and records state.
    /// </summary>
    /// <param name="patchName">Case-sensitive patch name.</param>
    /// <param name="isReplaying">Whether the workflow is currently being replayed.</param>
    /// <returns>True/false per replay semantics.</returns>
    public bool RequestPatch(string patchName, bool isReplaying)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchName);

        if (this.IsStalled)
            return false;

        if (_appliedPatches.TryGetValue(patchName, out var patched))
        {
            if (!patched)
                return patched;

            _patchesThisTurn.Add(patchName);
            _includeVersionInNextResponse = true;

            return patched;
        }

        if (_historyPatches.Contains(patchName))
        {
            patched = true;
        }
        else if (isReplaying)
        {
            patched = false;
        }
        else
        {
            patched = true;
        }

        _appliedPatches[patchName] = patched;

        if (patched)
        {
            _patchesThisTurn.Add(patchName);
            _includeVersionInNextResponse = true;
        }

        return patched;
    }

    /// <summary>
    /// Produces the <see cref="OrchestrationVersion"/> to stamp into the <see cref="OrchestratorResponse"/>.
    /// </summary>
    /// <param name="workflowName">The name of the current workflow.</param>
    /// <returns>An instance of a <see cref="OrchestrationVersion"/>.</returns>
    public OrchestrationVersion BuildResponseVersion(string workflowName) => new()
    {
        Name = workflowName,
        Patches = { _patchesThisTurn } 
    };
    
    private static List<string> ListAllVersioningPatches(IReadOnlyList<HistoryEvent> events)
    {
        var result = new List<string>();

        foreach (var ev in events)
        {
            var version = ev.OrchestratorStarted?.Version;
            if (version is not null)
            {
                result.AddRange(version.Patches);
            }
        }

        return result;
    }
}
