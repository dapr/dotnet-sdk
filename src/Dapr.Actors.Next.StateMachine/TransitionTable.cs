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

using System.Text.Json.Serialization;

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Serializable transition table produced by the compiled DSL.
/// </summary>
public sealed record TransitionTable<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the configured initial state.
    /// </summary>
    public TState? InitialState { get; init; }

    /// <summary>
    /// Gets the configured states.
    /// </summary>
    public IReadOnlyList<StateNode<TState>> States { get; init; } = [];

    /// <summary>
    /// Gets the configured event transitions.
    /// </summary>
    public IReadOnlyList<TransitionNode<TState>> Transitions { get; init; } = [];

    /// <summary>
    /// Gets the globally configured unhandled-event behavior.
    /// </summary>
    public BehaviorRef? Unhandled { get; init; }

    /// <summary>
    /// Gets the executable runtime state map used by the compiled state-machine runtime.
    /// </summary>
    internal IReadOnlyDictionary<TState, RuntimeState<TState, TData>> RuntimeStates { get; init; } = new Dictionary<TState, RuntimeState<TState, TData>>();
}

/// <summary>
/// Serializable state node in a transition table.
/// </summary>
public sealed record StateNode<TState>(
    TState State,
    TState? Parent,
    TimeSpan? Timeout,
    IReadOnlyList<BehaviorRef> Entry,
    IReadOnlyList<BehaviorRef> Exit)
    where TState : struct, Enum;

/// <summary>
/// Serializable transition node in a transition table.
/// </summary>
public sealed record TransitionNode<TState>(
    TState Source,
    string EventType,
    bool Ignored,
    bool Deferred,
    IReadOnlyList<GuardBranchNode<TState>> Branches)
    where TState : struct, Enum;

/// <summary>
/// Serializable guarded branch in a transition table.
/// </summary>
public sealed record GuardBranchNode<TState>(
    string? Guard,
    bool Otherwise,
    TState? Target,
    bool Internal,
    IReadOnlyList<BehaviorRef> Effects,
    object? Reply)
    where TState : struct, Enum;

/// <summary>
/// Serializable behavior reference for delegates or named capabilities.
/// </summary>
public sealed record BehaviorRef(string? Name, string? DelegateMethod)
{
    /// <summary>
    /// Creates a serializable reference for a delegate.
    /// </summary>
    public static BehaviorRef ForDelegate(Delegate value) => new(null, value.Method.Name);

    /// <summary>
    /// Creates a serializable reference for a named capability.
    /// </summary>
    public static BehaviorRef ForName(string name) => new(name, null);
}

/// <summary>
/// Executable metadata for one configured state.
/// </summary>
internal sealed class RuntimeState<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the state value represented by this node.
    /// </summary>
    public required TState State { get; init; }

    /// <summary>
    /// Gets or sets the optional parent state used for hierarchy bubbling.
    /// </summary>
    public TState? Parent { get; set; }

    /// <summary>
    /// Gets or sets the declarative timeout scheduled when this state is entered.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets the actions run when entering this state during a live transition.
    /// </summary>
    public List<RuntimeAction<TState, TData, object>> EntryActions { get; } = [];

    /// <summary>
    /// Gets the actions run when exiting this state during a live transition.
    /// </summary>
    public List<RuntimeAction<TState, TData, object>> ExitActions { get; } = [];

    /// <summary>
    /// Gets event handlers keyed by event type.
    /// </summary>
    public Dictionary<Type, RuntimeHandler<TState, TData>> Handlers { get; } = [];

    /// <summary>
    /// Gets event types ignored in this state.
    /// </summary>
    public HashSet<Type> IgnoredEvents { get; } = [];

    /// <summary>
    /// Gets event types durably deferred in this state.
    /// </summary>
    public HashSet<Type> DeferredEvents { get; } = [];
}

/// <summary>
/// Executable metadata for one event handler and its guard chain.
/// </summary>
internal sealed class RuntimeHandler<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the event type handled by this runtime handler.
    /// </summary>
    public required Type EventType { get; init; }

    /// <summary>
    /// Gets the ordered guard branches for this event handler.
    /// </summary>
    public List<RuntimeBranch<TState, TData>> Branches { get; } = [];
}

/// <summary>
/// Executable metadata for one guarded transition branch.
/// </summary>
internal sealed class RuntimeBranch<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Gets or sets the named guard capability used by this branch.
    /// </summary>
    public string? GuardName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this branch is the guard-chain fallthrough.
    /// </summary>
    public bool Otherwise { get; set; }

    /// <summary>
    /// Gets or sets the external transition target, if any.
    /// </summary>
    public TState? Target { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this branch is an internal transition.
    /// </summary>
    public bool IsInternal { get; set; } = true;

    /// <summary>
    /// Gets or sets the static reply value supplied by the branch.
    /// </summary>
    public object? ReplyValue { get; set; }

    /// <summary>
    /// Gets the ordered effects run by this branch.
    /// </summary>
    public List<object> Effects { get; } = [];

    /// <summary>
    /// Gets or sets the compiled guard delegate used by this branch.
    /// </summary>
    [JsonIgnore]
    public Delegate? Guard { get; set; }
}

/// <summary>
/// Executable wrapper for a compiled or named action.
/// </summary>
internal sealed class RuntimeAction<TState, TData, TEvent>(object action)
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the compiled delegate or named capability reference.
    /// </summary>
    public object Action { get; } = action;
}
