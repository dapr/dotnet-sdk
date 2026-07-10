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

using System.Reflection;

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Analyzes compiled state-machine transition tables for structural defects.
/// </summary>
public static class StateMachineAnalyzer
{
    /// <summary>
    /// Analyzes a state-machine actor type.
    /// </summary>
    public static StateMachineStructuralAnalysis Analyze(Type actorType)
    {
        ArgumentNullException.ThrowIfNull(actorType);

        var baseType = FindStateMachineBase(actorType)
            ?? throw new InvalidOperationException($"Actor type '{actorType.FullName}' does not derive from StateMachineActor<TState,TData>.");
        var stateType = baseType.GetGenericArguments()[0];
        var dataType = baseType.GetGenericArguments()[1];
        var method = typeof(StateMachineAnalyzer)
            .GetMethod(nameof(AnalyzeCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(stateType, dataType);
        return (StateMachineStructuralAnalysis)method.Invoke(null, [actorType])!;
    }

    private static StateMachineStructuralAnalysis AnalyzeCore<TState, TData>(Type actorType)
        where TState : struct, Enum
    {
        var table = StateMachineActor<TState, TData>.BuildDefinitionFor(actorType);
        var defects = new List<string>();

        if (!table.InitialState.HasValue)
        {
            defects.Add("Initial state is not configured.");
            return new StateMachineStructuralAnalysis(actorType, defects);
        }

        var configured = table.RuntimeStates.Keys.ToHashSet();
        foreach (var value in Enum.GetValues<TState>())
        {
            if (!configured.Contains(value))
            {
                defects.Add($"State '{value}' is not configured.");
            }
        }

        foreach (var state in table.RuntimeStates.Values)
        {
            if (state.Parent.HasValue && !configured.Contains(state.Parent.Value))
            {
                defects.Add($"State '{state.State}' declares missing parent '{state.Parent.Value}'.");
            }

            if (HasParentCycle(table, state.State))
            {
                defects.Add($"State '{state.State}' participates in a parent hierarchy cycle.");
            }

            foreach (var handler in state.Handlers.Values)
            {
                if (handler.Branches.Count == 0)
                {
                    defects.Add($"State '{state.State}' handler for '{handler.EventType.Name}' has no reachable branch.");
                }
                else if (handler.Branches.Any(branch => !branch.Otherwise) && handler.Branches.All(branch => !branch.Otherwise))
                {
                    defects.Add($"State '{state.State}' guard chain for '{handler.EventType.Name}' has no Otherwise branch.");
                }

                foreach (var target in handler.Branches.Where(branch => branch.Target.HasValue).Select(branch => branch.Target!.Value))
                {
                    if (!configured.Contains(target))
                    {
                        defects.Add($"State '{state.State}' transitions to unconfigured state '{target}'.");
                    }
                }
            }
        }

        foreach (var unreachable in configured.Except(ReachableStates(table)).OrderBy(state => state.ToString(), StringComparer.Ordinal))
        {
            defects.Add($"State '{unreachable}' is unreachable from initial state '{table.InitialState.Value}'.");
        }

        foreach (var deadEnd in configured.Where(state => IsDeadEnd(table, state)).OrderBy(state => state.ToString(), StringComparer.Ordinal))
        {
            defects.Add($"State '{deadEnd}' is a dead end.");
        }

        return new StateMachineStructuralAnalysis(actorType, defects.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static Type? FindStateMachineBase(Type actorType)
    {
        for (var current = actorType; current is not null; current = current.BaseType!)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StateMachineActor<,>))
            {
                return current;
            }
        }

        return null;
    }

    private static bool HasParentCycle<TState, TData>(TransitionTable<TState, TData> table, TState state)
        where TState : struct, Enum
    {
        var seen = new HashSet<TState>();
        var current = state;
        while (table.RuntimeStates.TryGetValue(current, out var node) && node.Parent.HasValue)
        {
            if (!seen.Add(current))
            {
                return true;
            }

            current = node.Parent.Value;
        }

        return false;
    }

    private static HashSet<TState> ReachableStates<TState, TData>(TransitionTable<TState, TData> table)
        where TState : struct, Enum
    {
        var reachable = new HashSet<TState>();
        var pending = new Queue<TState>();
        pending.Enqueue(table.InitialState!.Value);

        while (pending.Count > 0)
        {
            var state = pending.Dequeue();
            if (!reachable.Add(state) || !table.RuntimeStates.TryGetValue(state, out var node))
            {
                continue;
            }

            if (node.Parent.HasValue)
            {
                pending.Enqueue(node.Parent.Value);
            }

            foreach (var target in node.Handlers.Values.SelectMany(handler => handler.Branches).Where(branch => branch.Target.HasValue).Select(branch => branch.Target!.Value))
            {
                pending.Enqueue(target);
            }
        }

        return reachable;
    }

    private static bool IsDeadEnd<TState, TData>(TransitionTable<TState, TData> table, TState state)
        where TState : struct, Enum
    {
        if (!table.RuntimeStates.TryGetValue(state, out var node))
        {
            return false;
        }

        return node.Timeout is null
            && node.Handlers.Count == 0
            && node.IgnoredEvents.Count == 0
            && node.DeferredEvents.Count == 0;
    }
}
