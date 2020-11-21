// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// A state object created by an implementation of <see cref="ActorActivator" />. Implementations
    /// can return a subclass of <see cref="ActorActivatorState" /> to associate additional data
    /// with an Actor instance.
    /// </summary>
    public class ActorActivatorState
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ActorActivatorState" />.
        /// </summary>
        /// <param name="actor">The <see cref="Actor" /> instance.</param>
        public ActorActivatorState(Actor actor)
        {
            Actor = actor;
        }

        /// <summary>
        /// Gets the <see cref="Actor" /> instance.
        /// </summary>
        public Actor Actor { get; }
    }
}
