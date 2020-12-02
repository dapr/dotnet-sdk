// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// Represents an actor type registered with the runtime. Provides access to per-type
    /// options for the actor.
    /// </summary>
    public sealed class ActorRegistration
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ActorRegistration" />.
        /// </summary>
        /// <param name="type">The <see cref="ActorTypeInformation" /> for the actor type.</param>
        public ActorRegistration(ActorTypeInformation type)
        {
            this.Type = type;
        }

        /// <summary>
        /// Gets the <see cref="ActorTypeInformation" /> for the actor type.
        /// </summary>
        public ActorTypeInformation Type { get; }

        /// <summary>
        /// Gets or sets the <see cref="ActorActivator" /> to use for the actor. If not set the default
        /// activator of the runtime will be used.
        /// </summary>
        public ActorActivator Activator { get; set; }
    }
}
