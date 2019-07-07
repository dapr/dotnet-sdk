// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the interface that an actor state provider needs to implement for
    /// actor runtime to communicate with it.
    /// </summary>
    internal interface IActorStateProvider
    {
        /// <summary>
        /// Loads the actor state associated with the specified state name for the specified actor ID.
        /// </summary>
        /// <typeparam name="T">Type of value of actor state associated with given state name.</typeparam>
        /// <param name="actorId">ID of the actor for which to load the state.</param>
        /// <param name="stateName">Name of the actor state to load.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="KeyNotFoundException">Actor state associated with specified state name does not exist.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <returns>
        /// A task that represents the asynchronous load operation. The value of TResult
        /// parameter contains value of actor state associated with given state name.
        /// </returns>
        Task<T> LoadStateAsync<T>(ActorId actorId, string stateName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves the specified set of actor state changes for the specified actor ID atomically.
        /// </summary>
        /// <param name="actorId">ID of the actor for which to save the state changes.</param>
        /// <param name="stateChanges">Collection of state changes to save.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        /// <remarks>
        /// The collection of state changes should contain only one item for a given state name.
        /// The save operation will fail on trying to add an actor state which already exists
        /// or update/remove an actor state which does not exist.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">
        /// When <see cref="StateChangeKind"/> is <see cref="StateChangeKind.None"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        Task SaveStateAsync(ActorId actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Removes all the existing states and reminders associated with specified actor ID atomically.
        /// </summary>
        /// <param name="actorId">ID of the actor for which to remove state.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous remove operation.</returns>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        Task RemoveActorAsync(ActorId actorId, CancellationToken cancellationToken = default(CancellationToken));

        // TODO: Add Reminder Save/Delete methods.
    }
}
