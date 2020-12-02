// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// A default implementation of <see cref="ActorActivatorFactory" /> that uses <see cref="DefaultActorActivator" />.
    /// </summary>
    public class DefaultActorActivatorFactory : ActorActivatorFactory
    {
        /// <summary>
        /// Creates the <see cref="ActorActivator" /> for the provided <paramref name="type" />.
        /// </summary>
        /// <param name="type">The <see cref="ActorTypeInformation" />.</param>
        /// <returns>An <see cref="ActorActivator" />.</returns>
        public override ActorActivator CreateActivator(ActorTypeInformation type)
        {
            return new DefaultActorActivator();
        }
    }
}
