// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;

    /// <summary>
    /// Represents a host for an actor type within the aCtor runtime.
    /// </summary>
    public class ActorService : IActorService
    {
        private Func<ActorService, ActorId, Actor> actorFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorService"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="actorFactory">The factory method to create Actor objects.</param>
        public ActorService(
            ActorTypeInformation actorTypeInfo,
            Func<ActorService, ActorId, Actor> actorFactory = null)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.actorFactory = actorFactory ?? this.DefaultActorFactory;
            this.StateProvider = new ActionsStateProvider(new ActorStateProviderSerializer());
        }

        /// <summary>
        /// Gets the ActorTypeInformation for actor service.
        /// </summary>
        public ActorTypeInformation ActorTypeInfo { get; }

        internal ActionsStateProvider StateProvider { get; }

        internal Actor CreateActor(ActorId actorId)
        {
            return this.actorFactory.Invoke(this, actorId);
        }

        private Actor DefaultActorFactory(ActorService actorService, ActorId actorId)
        {
            return (Actor)Activator.CreateInstance(
                this.ActorTypeInfo.ImplementationType,
                actorService,
                actorId);
        }
    }
}
