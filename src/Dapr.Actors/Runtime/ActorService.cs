// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a host for an actor type within the actor runtime.
    /// </summary>
    public class ActorService : IActorService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Func<ActorService, ActorId, Actor> actorFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorService"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="actorFactory">The factory method to create Actor objects.</param>
        public ActorService(
            ActorTypeInformation actorTypeInfo,
            ILoggerFactory loggerFactory,
            Func<ActorService, ActorId, Actor> actorFactory = null)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.actorFactory = actorFactory ?? this.DefaultActorFactory;
            this.StateProvider = new DaprStateProvider();
            this.LoggerFactory = loggerFactory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorService"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="serviceProvider">The service container required to activate new Actor's instances.</param>
        /// <param name="actorFactory">The factory method to create Actor objects.</param>
        public ActorService(
            ActorTypeInformation actorTypeInfo,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            Func<ActorService, ActorId, Actor> actorFactory = null) :
            this(
                actorTypeInfo, 
                loggerFactory, 
                actorFactory ?? ServiceProviderBasedFactory(actorTypeInfo, serviceProvider))
        {
            this.serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Gets the ActorTypeInformation for actor service.
        /// </summary>
        public ActorTypeInformation ActorTypeInfo { get; }

        internal DaprStateProvider StateProvider { get; }

        /// <summary>
        /// Returns the instance of <see cref="IServiceProvider"/> used to activate new Actor's instances
        /// </summary>
        public IServiceProvider ServiceProvider => serviceProvider;

        /// <summary>
        /// Gets the LoggerFactory for actor service
        /// </summary>
        public ILoggerFactory LoggerFactory { get; private set; }

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

        private static Func<ActorService, ActorId, Actor> ServiceProviderBasedFactory(
            ActorTypeInformation actorTypeInfo,
            IServiceProvider serviceProvider)
        {
            return (actorService, actorId) =>
                (Actor)ActivatorUtilities.CreateInstance(
                    serviceProvider,
                    actorTypeInfo.ImplementationType,
                    actorService,
                    actorId);
        }
    }
}
