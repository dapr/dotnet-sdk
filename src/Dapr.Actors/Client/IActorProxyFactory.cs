// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;

namespace Dapr.Actors.Client
{
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
        /// <param name="options">The optional <see cref="ActorProxyOptions" /> to use when creating the actor proxy.</param>
        /// <returns>An actor proxy object that implements IActorProxy and TActorInterface.</returns>
        TActorInterface CreateActorProxy<TActorInterface>(
            ActorId actorId,
            string actorType,
            ActorProxyOptions options = null)
            where TActorInterface : IActor;

        /// <summary>
        /// Create a proxy, this method is also used by ActorReference also to create proxy.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorInterfaceType">Actor Interface Type.</param>
        /// <param name="actorType">Actor implementation Type.</param>
        /// <param name="options">The optional <see cref="ActorProxyOptions" /> to use when creating the actor proxy.</param>
        /// <returns>Returns Actor Proxy.</returns>
        object CreateActorProxy(ActorId actorId, Type actorInterfaceType, string actorType, ActorProxyOptions options = null);

        /// <summary>
        /// Creates an Actor Proxy for making calls without Remoting.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="options">The optional <see cref="ActorProxyOptions" /> to use when creating the actor proxy.</param>
        /// <returns>Actor proxy to interact with remote actor object.</returns>
        ActorProxy Create(ActorId actorId, string actorType, ActorProxyOptions options = null);
    }
}
