// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    /// <summary>
    /// Provides the interface for implementation of proxy access for actor service.
    /// </summary>
    public interface IActorProxy
    {
        /// <summary>
        /// Gets <see cref="Dapr.Actors.ActorId"/> associated with the proxy object.
        /// </summary>
        /// <value><see cref="Dapr.Actors.ActorId"/> associated with the proxy object.</value>
        ActorId ActorId { get; }

        /// <summary>
        /// Gets actor implementation type of the actor associated with the proxy object.
        /// </summary>
        /// <value>Actor implementation type of the actor associated with the proxy object.</value>
        string ActorType { get; }
    }
}
