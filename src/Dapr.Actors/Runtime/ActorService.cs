// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;

    /// <summary>
    /// Represents a host for an actor type within the actor runtime.
    /// </summary>
    public class ActorService : IActorService
    {
        private readonly Func<Actor> actorFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorService"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="actorFactory">The factory method to create Actor objects.</param>
        public ActorService(
            ActorTypeInformation actorTypeInfo,
            Func<Actor> actorFactory)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.actorFactory = actorFactory;
            this.StateProvider = new DaprStateProvider();
        }

        /// <summary>
        /// Gets the ActorTypeInformation for actor service.
        /// </summary>
        public ActorTypeInformation ActorTypeInfo { get; }

        internal DaprStateProvider StateProvider { get; }

        internal Actor CreateActor()
        {
            return this.actorFactory();
        }
    }
}
