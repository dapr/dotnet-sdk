﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Runtime;

    /// <summary>
    /// Interface for interacting with Actions runtime.
    /// </summary>
    internal interface IActionsInteractor
    {
        /// <summary>
        /// Invokes an Actor method on Actions without remoting.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="methodName">Method name to invoke.</param>
        /// <param name="jsonPayload">State changes.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<string> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves Actor state. This is temporary until the Actions runtime implements the Batch state update.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">state name.</param>
        /// <param name="data">State to be saved.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateAsync(string actorType, string actorId, string keyName, string data, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Removes Actor state. This is temporary until the Actions runtime implements the Batch state update.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">state name.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task RemoveStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves state batch to Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="stateChanges">State changes.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateBatchAsync(string actorType, string actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="keyName">Name of key to get value for.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<byte[]> GetStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Invokes Actor method.
        /// </summary>
        /// <param name="remotingRequestRequestMessage">Actor Request Message.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task<IActorResponseMessage> InvokeActorMethodWithRemotingAsync(IActorRequestMessage remotingRequestRequestMessage, CancellationToken cancellationToken = default(CancellationToken));
    }
}
