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

using System.Text.Json;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Serializable machine-definition document executed by the interpreted actor.
/// </summary>
public sealed record InterpretedMachineDefinition
{
    /// <summary>
    /// Gets the machine-definition document version.
    /// </summary>
    public int DocumentVersion { get; init; } = 1;

    /// <summary>
    /// Gets the initial state for a fresh actor instance.
    /// </summary>
    public required string InitialState { get; init; }

    /// <summary>
    /// Gets the states in the machine.
    /// </summary>
    public IReadOnlyList<InterpretedStateDefinition> States { get; init; } = [];

    /// <summary>
    /// Gets the event transitions in the machine.
    /// </summary>
    public IReadOnlyList<InterpretedTransitionDefinition> Transitions { get; init; } = [];

    /// <summary>
    /// Gets the initial dynamic state values for a fresh actor instance.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> InitialData { get; init; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}

/// <summary>
/// Serializable state node in an interpreted machine-definition document.
/// </summary>
public sealed record InterpretedStateDefinition
{
    /// <summary>
    /// Gets the state name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the state intentionally has no outgoing transitions.
    /// </summary>
    public bool Terminal { get; init; }

    /// <summary>
    /// Gets named effects to run when the state is entered.
    /// </summary>
    public IReadOnlyList<string> EntryEffects { get; init; } = [];

    /// <summary>
    /// Gets named effects to run when the state is exited.
    /// </summary>
    public IReadOnlyList<string> ExitEffects { get; init; } = [];
}

/// <summary>
/// Serializable event transition in an interpreted machine-definition document.
/// </summary>
public sealed record InterpretedTransitionDefinition
{
    /// <summary>
    /// Gets the source state.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the event name handled by this transition.
    /// </summary>
    public required string Event { get; init; }

    /// <summary>
    /// Gets the ordered guarded branches.
    /// </summary>
    public IReadOnlyList<InterpretedBranchDefinition> Branches { get; init; } = [];
}

/// <summary>
/// Serializable guarded branch in an interpreted transition.
/// </summary>
public sealed record InterpretedBranchDefinition
{
    /// <summary>
    /// Gets the named guard capabilities that must all pass.
    /// </summary>
    public IReadOnlyList<string> Guards { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether this branch is the guard fallthrough.
    /// </summary>
    public bool Otherwise { get; init; }

    /// <summary>
    /// Gets the optional transition target. A null value is an internal transition.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets the named effects to execute.
    /// </summary>
    public IReadOnlyList<string> Effects { get; init; } = [];

    /// <summary>
    /// Gets the static JSON reply value.
    /// </summary>
    public JsonElement? Reply { get; init; }
}
