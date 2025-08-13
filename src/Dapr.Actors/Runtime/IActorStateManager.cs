// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors.Runtime;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents an interface that exposes methods to manage state of an <see cref="Dapr.Actors.Runtime.Actor" />.
/// This interface is implemented by <see cref="Dapr.Actors.Runtime.Actor.StateManager"/>.
/// </summary>
public interface IActorStateManager
{
    /// <summary>
    /// Adds an actor state with given state name.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to add.</param>
    /// <param name="value">Value of the actor state to add.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous add operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// An actor state with given state name already exists.
    /// </exception>
    /// <exception cref="ArgumentNullException">The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an actor state with given state name.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to add.</param>
    /// <param name="value">Value of the actor state to add.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <param name="ttl">The time to live for the state.</param>
    /// <returns>
    /// A task that represents the asynchronous add operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// An actor state with given state name already exists.
    /// </exception>
    /// <exception cref="ArgumentNullException">The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task AddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an actor state with specified state name.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to get.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous get operation. The value of TResult
    /// parameter contains value of actor state with given state name.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// An actor state with given state name does not exist.
    /// </exception>
    /// <exception cref="ArgumentNullException">The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an actor state with given state name to specified value.
    /// If an actor state with specified name does not exist, it is added.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to set.</param>
    /// <param name="value">Value of the actor state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous set operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an actor state with given state name to specified value.
    /// If an actor state with specified name does not exist, it is added.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to set.</param>
    /// <param name="value">Value of the actor state.</param>
    /// <param name="ttl">The time to live for the state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous set operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task SetStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an actor state with specified state name.
    /// </summary>
    /// <param name="stateName">Name of the actor state to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// An actor state with given state name does not exist.
    /// </exception>
    /// <exception cref="ArgumentNullException"> The specified state name is null. </exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to add an actor state with given state name and value. Returns false if an actor state with
    /// the same name already exists.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to add.</param>
    /// <param name="value">Value of the actor state to add.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.
    /// This is optional and defaults to <see cref="System.Threading.CancellationToken.None" />.</param>
    /// <returns>
    /// A boolean task that represents the asynchronous add operation. Returns true if the
    /// value was successfully added and false if an actor state with the same name already exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">The specified state name is null.
    /// Provide a valid state name string.</exception>
    /// <exception cref="OperationCanceledException">The request was canceled using the specified
    /// <paramref name="cancellationToken" />.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to add an actor state with given state name and value. Returns false if an actor state with
    /// the same name already exists.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to add.</param>
    /// <param name="value">Value of the actor state to add.</param>
    /// <param name="ttl">The time to live for the state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.
    /// This is optional and defaults to <see cref="System.Threading.CancellationToken.None" />.</param>
    /// <returns>
    /// A boolean task that represents the asynchronous add operation. Returns true if the
    /// value was successfully added and false if an actor state with the same name already exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">The specified state name is null.
    /// Provide a valid state name string.</exception>
    /// <exception cref="OperationCanceledException">The request was canceled using the specified
    /// <paramref name="cancellationToken" />.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<bool> TryAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to get an actor state with specified state name.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to get.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous get operation. The value of TResult
    /// parameter contains <see cref="ConditionalValue{TValue}"/>
    /// indicating whether the actor state is present and the value of actor state if it is present.
    /// </returns>
    /// <exception cref="ArgumentNullException">The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to remove an actor state with specified state name.
    /// </summary>
    /// <param name="stateName">Name of the actor state to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous remove operation. The value of TResult
    /// parameter indicates if the state was successfully removed.
    /// </returns>
    /// <exception cref="ArgumentNullException"> The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an actor state with specified name exists.
    /// </summary>
    /// <param name="stateName">Name of the actor state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous check operation. The value of TResult
    /// parameter is <c>true</c> if state with specified name exists otherwise <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"> The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an actor state with the given state name if it exists. If it does not
    /// exist, creates and new state with the specified name and value.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to get or add.</param>
    /// <param name="value">Value of the actor state to add if it does not exist.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous get or add operation. The value of TResult
    /// parameter contains value of actor state with given state name.
    /// </returns>
    /// <exception cref="ArgumentNullException"> The specified state name is null.
    /// Provide a valid state name string.</exception>
    /// <exception cref="OperationCanceledException">The request was canceled using the specified
    /// <paramref name="cancellationToken" />.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an actor state with the given state name if it exists. If it does not
    /// exist, creates and new state with the specified name and value.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to get or add.</param>
    /// <param name="value">Value of the actor state to add if it does not exist.</param>
    /// <param name="ttl">The time to live for the state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous get or add operation. The value of TResult
    /// parameter contains value of actor state with given state name.
    /// </returns>
    /// <exception cref="ArgumentNullException"> The specified state name is null.
    /// Provide a valid state name string.</exception>
    /// <exception cref="OperationCanceledException">The request was canceled using the specified
    /// <paramref name="cancellationToken" />.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<T> GetOrAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an actor state with given state name, if it does not already exist or updates
    /// the state with specified state name, if it exists.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to add or update.</param>
    /// <param name="addValue">Value of the actor state to add if it does not exist.</param>
    /// <param name="updateValueFactory">Factory function to generate value of actor state to update if it exists.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous add/update operation. The value of TResult
    /// parameter contains value of actor state that was added/updated.
    /// </returns>
    /// <exception cref="ArgumentNullException"> The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an actor state with given state name, if it does not already exist or updates
    /// the state with specified state name, if it exists.
    /// </summary>
    /// <typeparam name="T">Type of value associated with given state name.</typeparam>
    /// <param name="stateName">Name of the actor state to add or update.</param>
    /// <param name="addValue">Value of the actor state to add if it does not exist.</param>
    /// <param name="updateValueFactory">Factory function to generate value of actor state to update if it exists.</param>
    /// <param name="ttl">The time to live for the state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous add/update operation. The value of TResult
    /// parameter contains value of actor state that was added/updated.
    /// </returns>
    /// <exception cref="ArgumentNullException"> The specified state name is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// The type of state value <typeparamref name="T"/> must be
    /// <see href="https://msdn.microsoft.com/library/ms731923.aspx">Data Contract</see> serializable.
    /// </remarks>
    Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all the cached actor states and any operation(s) performed on <see cref="IActorStateManager"/>
    /// since last state save operation.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous clear cache operation.
    /// </returns>
    /// <remarks>
    /// All the operation(s) performed on <see cref="IActorStateManager"/>  since last save operation are cleared on
    /// clearing the cache and will not be included in next save operation.
    /// </remarks>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all the cached state changes (add/update/remove) that were made since last call to
    /// <see cref="IActorStateManager.SaveStateAsync"/> by actor runtime or by user explicitly.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous save operation.
    /// </returns>
    Task SaveStateAsync(CancellationToken cancellationToken = default);
}