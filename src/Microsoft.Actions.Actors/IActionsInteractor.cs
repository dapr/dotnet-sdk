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
    /// Interface for interacting with Actions runtime.
    /// </summary>
    internal interface IActionsInteractor
    {
        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="actorId">ActorId.</param>
        /// <param name="stateChanges">State changes.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveStateAsync(ActorId actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Saves a state to Actions.
        /// </summary>
        /// <param name="actorId">ActorId..</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<string> GetStateAsync(ActorId actorId, CancellationToken cancellationToken = default(CancellationToken));

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
