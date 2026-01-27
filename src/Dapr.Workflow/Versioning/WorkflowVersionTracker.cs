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
    public IReadOnlyList<string> AggregatedPatchesOrdered => _historyPatchSequence;

    /// <summary>
    /// Retrieves all the versioning patches from the list of history events, in order, preserving duplicates.
    /// </summary>
    private static List<string> ListAllVersioningPatches(List<HistoryEvent> events)
    {
        var result = new List<string>();
        
        foreach (var ev in events)
        {
            if (ev.OrchestratorStarted?.Version != null)
            {
                result.AddRange(ev.OrchestratorStarted.Version.Patches);
            }
        }

        return result;
    }

    /// <summary>
    /// Called at the start of each orchestrator turn with the OrchestratorStartedEvent.version.
    /// </summary>
    /// <remarks>
    /// The replay validation in this tracker is driven by the history-derived sequence and RequestPatch().
    /// This method intentionally does not enforce additional rules.
    /// </remarks>
    public void OnOrchestratorStarted(OrchestrationVersion? incrementalVersionFromRuntime)
    {
        if (this.IsStalled || incrementalVersionFromRuntime is null)
            return;
        
        // Replay determinism is validated via RequestPatch() ordering
        // and "missing patches" are validated via ValidateReplayConsumedHistoryPatches().
    }

    /// <summary>
    /// Validates that the replay has evaluated every patch recorded in history up to this decision point.
    /// If not, the workflow has changed (or code path diverged) and must stall.
    /// </summary>
    public void ValidateReplayConsumedHistoryPatches()
    {
        if (this.IsStalled)
            return;

        if (_replayIndex < _historyPatchSequence.Count)
        {
            var expectedNext = _historyPatchSequence[_replayIndex];
            this.StalledEvent = new ExecutionStalledEvent
            {
                Reason = StalledReason.PatchMismatch,
                Description =
                    $"Replay did not evaluate all historical patches. Next expected patch is '{expectedNext}' at index {_replayIndex} (history count={_historyPatchSequence.Count})."
            };
        }
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
            if (_replayIndex >= _historyPatchSequence.Count)
            {
                this.StalledEvent = new ExecutionStalledEvent
                {
                    Reason = StalledReason.PatchMismatch,
                    Description =
                        $"Replay evaluated patch '{patchName}' but history contains no patch at index {_replayIndex}."
                };
                return false;
            }

            var expected = _historyPatchSequence[_replayIndex];
            if (!string.Equals(expected, patchName, StringComparison.Ordinal))
            {
                this.StalledEvent = new ExecutionStalledEvent
                {
                    Reason = StalledReason.PatchMismatch,
                    Description =
                        $"Patch replay mismatch at index {_replayIndex}. Expected '{expected}' but evaluated '{patchName}'."
                };
                return false;
            }

            _replayIndex++;
            return true;
        }
        
        // Non-replay: record exactly what the code evaluated, including duplicates
        _patchesThisTurn.Add(patchName);
        _includeVersionInNextResponse = true;
        return true;
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
}
