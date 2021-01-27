// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    using System;
    using Dapr.Actors.Client;

    /// <summary>
    /// Interface for ActorReference.
    /// </summary>
    internal interface IActorReference
    {
        /// <summary>
        /// Creates an <see cref="ActorProxy"/> that implements an actor interface for the actor using the
        ///     <see cref="ActorProxyFactory.CreateActorProxy(Dapr.Actors.ActorId, System.Type, string, ActorProxyOptions)"/>
        /// method.
        /// </summary>
        /// <param name="actorInterfaceType">Actor interface for the created <see cref="ActorProxy"/> to implement.</param>
        /// <returns>An actor proxy object that implements <see cref="IActorProxy"/> and TActorInterface.</returns>
        object Bind(Type actorInterfaceType);
    }
}
