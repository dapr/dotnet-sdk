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

namespace Dapr.VirtualActors;

/// <summary>
/// Provides read and write access to the state of a virtual actor.
/// </summary>
/// <remarks>
/// The state manager persists state changes transactionally so that either all
/// changes succeed or none are applied. State is stored using the actor's identity
/// (type + ID) as the key prefix.
/// </remarks>
public interface IActorStateManager
{
    /// <summary>
    /// Adds a state entry with the specified key. Throws if the key already exists.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="stateName">The name of the state to add.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a state with <paramref name="stateName"/> already exists.
    /// </exception>
    Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the state value associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="stateName">The name of the state to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The state value.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown if no state with <paramref name="stateName"/> exists.
    /// </exception>
    Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to get the state value associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="stateName">The name of the state to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ConditionalValue{T}"/> containing the value if found.
    /// </returns>
    Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the state value for the specified key, creating or overwriting it.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="stateName">The name of the state to set.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a state entry with the specified key.
    /// </summary>
    /// <param name="stateName">The name of the state to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to remove a state entry with the specified key.
    /// </summary>
    /// <param name="stateName">The name of the state to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the state was found and removed; otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the state store contains a state with the specified key.
    /// </summary>
    /// <param name="stateName">The name of the state to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the state exists; otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending state changes to the underlying store as a single transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached state and clears all pending operations without making any
    /// changes to the underlying store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}
