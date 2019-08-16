// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an interface that exposes methods to manage state of an Actor.
    /// </summary>
    public interface IActorStateManager
    {
        /// <summary>
        /// Adds or updates an actor state with given state name.
        /// </summary>
        /// <typeparam name="T">Type of value associated with given state name.</typeparam>
        /// <param name="stateName">Name of the actor state to add or update.</param>
        /// <param name="value">Value of the actor state to add or update.</param>
        /// <returns>
        /// A task that represents the asynchronous add operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">The specified state name is null.</exception>        
        Task AddOrUpdateState<T>(string stateName, T value);

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
        Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default(CancellationToken));

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
        Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves all the cached state changes (add/update/remove) that were made since last call to
        /// <see cref="IActorStateManager.SaveStateAsync"/> by actor runtime or by user explicitly.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous save operation.
        /// </returns>
        Task SaveStateAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Clears all the cached actor states and any operation(s) performed on <see cref="IActorStateManager"/>
        /// since last state save operation.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous remove operation.</returns>
        /// <remarks>
        /// All the operation(s) performed on <see cref="IActorStateManager"/>  since last save operation are cleared on
        /// clearing the cache and will not be included in next save operation.
        /// </remarks>
        Task ClearCacheAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
