// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains the configuration for the ActorRuntime
    /// </summary>
    public class ActorRuntimeConfiguration
    {
        /// <summary>
        /// List of registered actors
        /// </summary>
        public List<ActorTypeInformation> ActorRegistrations { get; } = new List<ActorTypeInformation>();

        /// <summary>
        /// Registers an actor with the runtime.
        /// </summary>
        /// <typeparam name="TActor">Type of actor.</typeparam>
        public void RegisterActor<TActor>()
            where TActor : Actor
        {
            var actorTypeInfo = ActorTypeInformation.Get(typeof(TActor));

            ActorRegistrations.Add(actorTypeInfo);
        }
    }
}
