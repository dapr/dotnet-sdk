// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the Dapr runtime options
    /// </summary>
    public class DaprActorRuntimeOptions
    {
        // Map of ActorType --> ActorManager.
        internal readonly Dictionary<ActorTypeInformation, Func<ActorTypeInformation, ActorService>> actorServicesFunc = new Dictionary<ActorTypeInformation, Func<ActorTypeInformation, ActorService>>();

        /// <summary>
        /// Registers an actor with the runtime.
        /// </summary>
        /// <typeparam name="TActor">Type of actor.</typeparam>
        /// <param name="actorServiceFactory">An optional delegate to create actor service. This can be used for dependency injection into actors.</param>
        public void RegisterActor<TActor>(Func<ActorTypeInformation, ActorService> actorServiceFactory = null)
            where TActor : Actor
        {
            var actorTypeInfo = ActorTypeInformation.Get(typeof(TActor));
            this.actorServicesFunc.Add(actorTypeInfo, actorServiceFactory);
        }
    }
}
