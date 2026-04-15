// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Internal interface for actor state persistence operations against the Dapr sidecar.
/// </summary>
/// <remarks>
/// <para>
/// Actor state is managed by the Dapr runtime via its configured state store component.
/// This interface exists as an internal seam for unit testing — it is not a public
/// extensibility point because the state store is a runtime infrastructure concern.
/// </para>
/// </remarks>
internal interface IActorStateProvider
{
    /// <summary>
    /// Attempts to load a state value from the underlying store.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="stateName">The state key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ConditionalValue{T}"/> containing the value if it exists.
    /// </returns>
    Task<ConditionalValue<T>> TryLoadStateAsync<T>(
        string actorType,
        VirtualActorId actorId,
        string stateName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a batch of state changes as a single transaction.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="stateChanges">The state changes to apply.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveStateAsync(
        string actorType,
        VirtualActorId actorId,
        IReadOnlyList<ActorStateChange> stateChanges,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single state change operation within an actor state transaction.
/// </summary>
/// <param name="StateName">The name of the state key.</param>
/// <param name="Value">The value to store (for add/update), or <see langword="null"/> for removals.</param>
/// <param name="ChangeKind">The type of state operation.</param>
/// <param name="Ttl">Optional time-to-live for the state entry.</param>
internal sealed record ActorStateChange(
    string StateName,
    object? Value,
    StateChangeKind ChangeKind,
    TimeSpan? Ttl = null);

/// <summary>
/// Specifies the type of state change operation.
/// </summary>
internal enum StateChangeKind
{
    /// <summary>
    /// No change.
    /// </summary>
    None = 0,

    /// <summary>
    /// A new state entry is being added.
    /// </summary>
    Add = 1,

    /// <summary>
    /// An existing state entry is being updated.
    /// </summary>
    Update = 2,

    /// <summary>
    /// A state entry is being removed.
    /// </summary>
    Remove = 3,
}
