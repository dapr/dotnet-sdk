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
    // Aggregated across the workflow execution in first-seen order
    private readonly List<string> _aggregatedHistoryOrdered = [];
    private readonly HashSet<string> _aggregatedHistorySet = new(StringComparer.Ordinal);

    // Patches the current code has encountered (case-sensitive uniqueness)
    private readonly List<string> _encounteredByCodeOrdered = [];
    private readonly HashSet<string> _encounteredByCodeSet = new(StringComparer.Ordinal);

    // Per-turn flags
    private bool _includeVersionInNextResponse;

    // Stall state (if any).
    public bool IsStalled => this.StalledEvent != null;
    
    public ExecutionStalledEvent? StalledEvent { get; private set; }

    public bool IncludeVersionInNextResponse => _includeVersionInNextResponse;

    /// <summary>
    /// Aggregated, ordered patches to be stamped in OrchestratorResponse.version.
    /// </summary>
    public IReadOnlyList<string> AggregatedPatchesOrdered => _aggregatedHistoryOrdered;

    public WorkflowVersionTracker(List<HistoryEvent> events)
    {
        _aggregatedHistoryOrdered = ListAllVersioningPatches(events);
        foreach (var patch in _aggregatedHistoryOrdered)
        {
            _aggregatedHistorySet.Add(patch);
        }
    }

    /// <summary>
    /// Retrieves all the versioning patches from the list of history events.
    /// </summary>
    /// <param name="events"></param>
    /// <returns></returns>
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
    /// Called at the start of each orchestrator turn with the OrchestratorStartedEvent.version that contains
    /// the patches observed since the last replay.
    /// </summary>
    /// <param name="incrementalVersionFromRuntime"></param>
    public void OnOrchestratorStarted(OrchestrationVersion? incrementalVersionFromRuntime)
    {
        if (this.IsStalled || incrementalVersionFromRuntime is null)
            return;
        
        // The runtime sends only the *new* patches observed since the last replay. We need to 
        // merge them into the full aggregation in first-seen order
        foreach (var patch in incrementalVersionFromRuntime.Patches)
        {
            if (_aggregatedHistorySet.Add(patch))
                _aggregatedHistoryOrdered.Add(patch);
            
            // Mismatch rule: history shows a patch enabled that our *current* code has not yet enabled
            // up to this pointin the execution. This implies code moved or a version gap.
            if (!_encounteredByCodeSet.Contains(patch))
            {
                this.StalledEvent = new ExecutionStalledEvent
                {
                    Reason = StalledReason.PatchMismatch,
                    Description = $"History reports patch '{patch}' not yet enabled by current code path."
                };
                break;
            }
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
            // Once stalled, be conservative; caller should should-circuit this turn
            return false;

        // Replay semantics
        if (isReplaying)
        {
            var hasPatch = _aggregatedHistorySet.Contains(patchName);
            if (hasPatch)
            {
                _encounteredByCodeSet.Add(patchName);
                _encounteredByCodeOrdered.Add(patchName);
            }

            return hasPatch;
        }
        
        // Non-replay semantics: Encountering a patch means it is enabled in code now. Detect duplicate usage across
        // the current execution to signal a stall
        if (_encounteredByCodeSet.Contains(patchName))
        {
            this.StalledEvent = new ExecutionStalledEvent
            {
                Reason = StalledReason.PatchMismatch,
                Description = $"Duplicate patch '{patchName}' encountered by code. Patch names must be unique in a " +
                              "workflow execution."
            };
            return false;
        }

        _encounteredByCodeSet.Add(patchName);
        _encounteredByCodeOrdered.Add(patchName);
        
        // We include version info only if at least one patch is encountered this turn
        _includeVersionInNextResponse = true;
        
        // Non-replay scenarios always return true by design
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
        Patches = { _encounteredByCodeOrdered } // Ordered, de-duplicated 
    };
}
