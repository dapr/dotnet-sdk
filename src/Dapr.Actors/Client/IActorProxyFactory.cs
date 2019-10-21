// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

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
        /// <returns>An actor proxy object that implements IActorProxy and TActorInterface.</returns>
        TActorInterface CreateActorProxy<TActorInterface>(
            ActorId actorId,
            string actorType)
            where TActorInterface : IActor;

        /// <summary>
        /// Creates an Actor Proxy for making calls without Remoting.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorType">Type of actor.</param>
        /// <returns>Actor proxy to interact with remote actor object.</returns>
        ActorProxy Create(ActorId actorId, string actorType);
    }
}
