// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// An abstraction used to construct an <see cref="ActorActivator" /> for a given
    /// <see cref="ActorTypeInformation" />.
    /// </summary>
    public abstract class ActorActivatorFactory 
    {
        /// <summary>
        /// Creates the <see cref="ActorActivator" /> for the provided <paramref name="type" />.
        /// </summary>
        /// <param name="type">The <see cref="ActorTypeInformation" />.</param>
        /// <returns>An <see cref="ActorActivator" />.</returns>
        public abstract ActorActivator CreateActivator(ActorTypeInformation type);
    }
}
