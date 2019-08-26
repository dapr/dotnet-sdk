// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{
    using System;

    /// <summary>
    /// Defines the interface containing methods to create actor proxy factory class.
    /// </summary>
    public interface IActorProxyFactory
    {
        /// <summary>
        /// Creates a proxy to the actor object that implements an actor interface.
        /// </summary>
        /// <typeparam name="TActorInterface">
        /// The actor interface implemented by the remote actor object.
        /// The returned proxy object will implement this interface.
        /// </typeparam>
        /// <param name="actorId">Actor Id of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this id.</param>
        /// <param name="actorType">Type of actor implementation.</param>
        /// <returns>An actor proxy object that implements IActorProxy and TActorInterface.</returns>
        TActorInterface CreateActorProxy<TActorInterface>(
            ActorId actorId,
            string actorType)
            where TActorInterface : IActor;
    }
}
