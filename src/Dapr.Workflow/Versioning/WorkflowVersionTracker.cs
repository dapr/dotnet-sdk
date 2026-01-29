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
internal sealed class WorkflowVersionTracker(List<HistoryEvent> events)
{
    /// <summary>
    /// Full patch evaluation sequence as recorded by history (duplicates allowed, ordered).
    /// </summary>
    private readonly List<string> _historyPatchSequence = ListAllVersioningPatches(events);
    
    /// <summary>
    /// Cursor into _historyPatchSequence as the workflow code replays and evaluates patches.
    /// </summary>
    private int _replayIndex;

    /// <summary>
    /// Patches evaluated in the current non-replay execution (duplicates allowed, ordered).
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
    /// Aggregated, ordered patches extracted from history (with duplicates preserved).
    /// </summary>
    public IReadOnlyCollection<string> AggregatedPatchesOrdered => _historyPatchSequence;

    public void OnOrchestratorStarted()
    {
        if (this.IsStalled)
            return;

        _patchesThisTurn.Clear();
        _includeVersionInNextResponse = false;
    }
    
    /// <summary>
    /// Request enabling/using a patch from workflow code. Returns IsPatched result and records state.
    /// </summary>
    /// <param name="patchName">Case-sensitive patch name.</param>
    /// <param name="isReplaying">Whether the workflow is currently being replayed.</param>
    /// <returns>True/false per replay semantics; may set stall state on duplicate use.</returns>
    public bool RequestPatch(string patchName, bool isReplaying)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchName);

        if (this.IsStalled)
            return false;

        if (isReplaying)
        {
            // Strict replay semantics:
            // - patches must be evaluated in the same order as history
            // - duplicates must be evaluated the same number of times
            if (_replayIndex >= _historyPatchSequence.Count)
            {
                Stall(
                    $"Replay evaluated extra patch '{patchName}' but history is already fully consumed.");
                return false;
            }

            var expected = _historyPatchSequence[_replayIndex];
            if (!string.Equals(expected, patchName, StringComparison.Ordinal))
            {
                Stall($"Patch replay mismatch. Expected '{expected}' at index {_replayIndex}, got '{patchName}'.");
                return false;
            }

            _replayIndex++;
            return true;
        }

        // Non-replay semantics: patch evaluations are treated as true and stamped (including duplicates).
        _patchesThisTurn.Add(patchName);
        _includeVersionInNextResponse = true;
        return true;
    }
    
    /// <summary>
    /// Validates that replay evaluated all historical patches.
    /// If not, the workflow should stall.
    /// </summary>
    public void ValidateReplayConsumedHistoryPatches()
    {
        if (this.IsStalled)
            return;

        if (_replayIndex < _historyPatchSequence.Count)
        {
            Stall(
                $"Replay did not evaluate all historical patches. Consumed {_replayIndex} of {_historyPatchSequence.Count}.");
        }
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
    
    private void Stall(string description)
    {
        this.StalledEvent = new ExecutionStalledEvent
        {
            Reason = StalledReason.PatchMismatch,
            Description = description,
        };
    }

    private static List<string> ListAllVersioningPatches(IReadOnlyList<HistoryEvent> events)
    {
        var result = new List<string>();

        foreach (var ev in events)
        {
            if (ev.OrchestratorStarted?.Version is not null)
            {
                // IMPORTANT: keep order + duplicates
                result.AddRange(ev.OrchestratorStarted.Version.Patches);
            }
        }

        return result;
    }
}
