// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a host for an actor type within the actor runtime.
    /// </summary>
    public sealed class ActorHost
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorHost"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="id">The id of the Actor instance.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public ActorHost(
            ActorTypeInformation actorTypeInfo,
            ActorId id,
            ILoggerFactory loggerFactory)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.Id = id;
            this.LoggerFactory = loggerFactory;
            this.StateProvider = new DaprStateProvider();
        }

        /// <summary>
        /// Gets the ActorTypeInformation for actor service.
        /// </summary>
        public ActorTypeInformation ActorTypeInfo { get; }

        /// <summary>
        /// Gets the <see cref="ActorId" />.
        /// </summary>
        public ActorId Id { get; }

        internal DaprStateProvider StateProvider { get; }

        /// <summary>
        /// Gets the LoggerFactory for actor service
        /// </summary>
        public ILoggerFactory LoggerFactory { get; }
    }
}
