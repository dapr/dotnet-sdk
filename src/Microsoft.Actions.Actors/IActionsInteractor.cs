// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;

    /// <summary>
    /// Interface for interacting with Actions runtime.
    /// </summary>
    internal interface IActionsInteractor
    {
        /// <summary>
        /// Invokes an Actor method on Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="methodName">Method name to invoke.</param>
        /// <param name="jsonPayload">State changes.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<string> InvokeActorMethodAsync(string actorType, ActorId actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="stateChanges">State changes.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateAsync(Type actorType, ActorId actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">Name of key to get value for.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<string> GetStateAsync(Type actorType, ActorId actorId, string keyName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Invokes Actor method.
        /// </summary>
        /// <param name="actorId">ActorId.</param>
        /// <param name="actorType">Actor Type.</param>
        /// <param name="methodName">Method Name.</param>
        /// <param name="messageHeader">Message Header.</param>
        /// <param name="messageBody">Message Body.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task<object> InvokeActorMethod(string actorId, string actorType, string methodName, byte[] messageHeader, byte[] messageBody, CancellationToken cancellationToken = default(CancellationToken));        
    }
}
