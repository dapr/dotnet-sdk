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

using Dapr.Actors.Next.Abstractions.Filters;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Default verifier for interpreted machine-definition documents.
/// </summary>
public sealed class InterpretedMachineVerifier(ICapabilityRegistry capabilities) : IInterpretedMachineVerifier
{
    /// <inheritdoc />
    public InterpretedMachineVerificationResult Verify(InterpretedMachineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var defects = new List<string>();
        if (definition.DocumentVersion <= 0)
        {
            defects.Add("DocumentVersion must be positive.");
        }

        if (string.IsNullOrWhiteSpace(definition.InitialState))
        {
            defects.Add("InitialState is required.");
        }

        var states = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in definition.States)
        {
            if (string.IsNullOrWhiteSpace(state.Name))
            {
                defects.Add("State names cannot be empty.");
                continue;
            }

            if (!states.Add(state.Name))
            {
                defects.Add($"State '{state.Name}' is duplicated.");
            }

            VerifyEffects($"state '{state.Name}' entry", state.EntryEffects, defects);
            VerifyEffects($"state '{state.Name}' exit", state.ExitEffects, defects);
        }

        if (!string.IsNullOrWhiteSpace(definition.InitialState) && !states.Contains(definition.InitialState))
        {
            defects.Add($"InitialState '{definition.InitialState}' is not declared.");
        }

        var outgoing = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var transition in definition.Transitions)
        {
            VerifyTransition(transition, states, outgoing, defects);
        }

        foreach (var state in definition.States)
        {
            if (!state.Terminal && !outgoing.ContainsKey(state.Name))
            {
                defects.Add($"State '{state.Name}' is a dead end and is not terminal.");
            }
        }

        AddUnreachableDefects(definition, states, defects);
        return new InterpretedMachineVerificationResult(defects);
    }

    private void VerifyTransition(
        InterpretedTransitionDefinition transition,
        HashSet<string> states,
        Dictionary<string, int> outgoing,
        List<string> defects)
    {
        if (string.IsNullOrWhiteSpace(transition.Source))
        {
            defects.Add("Transition source is required.");
        }
        else if (!states.Contains(transition.Source))
        {
            defects.Add($"Transition source '{transition.Source}' is not declared.");
        }
        else
        {
            outgoing[transition.Source] = outgoing.GetValueOrDefault(transition.Source) + 1;
        }

        if (string.IsNullOrWhiteSpace(transition.Event))
        {
            defects.Add($"Transition from '{transition.Source}' has an empty event name.");
        }

        if (transition.Branches.Count == 0)
        {
            defects.Add($"Transition '{transition.Source}/{transition.Event}' has no branches.");
        }

        foreach (var branch in transition.Branches)
        {
            if (!branch.Otherwise && branch.Guards.Count == 0)
            {
                defects.Add($"Transition '{transition.Source}/{transition.Event}' has a branch with no guard and no otherwise fallthrough.");
            }

            if (branch.Target is not null && !states.Contains(branch.Target))
            {
                defects.Add($"Transition '{transition.Source}/{transition.Event}' targets undeclared state '{branch.Target}'.");
            }

            foreach (var guard in branch.Guards)
            {
                if (!capabilities.TryGetGuard(guard, out _))
                {
                    defects.Add($"Guard '{guard}' is not registered.");
                }
            }

            VerifyEffects($"transition '{transition.Source}/{transition.Event}'", branch.Effects, defects);
        }
    }

    private void VerifyEffects(string owner, IReadOnlyList<string> effects, List<string> defects)
    {
        foreach (var effect in effects)
        {
            if (!capabilities.TryGetEffect(effect, out _))
            {
                defects.Add($"Effect '{effect}' referenced by {owner} is not registered.");
            }
        }
    }

    private static void AddUnreachableDefects(InterpretedMachineDefinition definition, HashSet<string> states, List<string> defects)
    {
        if (string.IsNullOrWhiteSpace(definition.InitialState) || !states.Contains(definition.InitialState))
        {
            return;
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal) { definition.InitialState };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var transition in definition.Transitions)
            {
                if (!reachable.Contains(transition.Source))
                {
                    continue;
                }

                foreach (var branch in transition.Branches)
                {
                    if (branch.Target is not null && reachable.Add(branch.Target))
                    {
                        changed = true;
                    }
                }
            }
        }

        foreach (var state in states.Except(reachable, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            defects.Add($"State '{state}' is unreachable.");
        }
    }
}
