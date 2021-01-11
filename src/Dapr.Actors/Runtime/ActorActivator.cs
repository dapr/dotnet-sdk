// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// An abstraction for implementing the creation and deletion of actor instances.
    /// </summary>
    public abstract class ActorActivator
    {
        /// <summary>
        /// Creates the actor instance and returns it inside an instance of <see cref="ActorActivatorState" />.
        /// </summary>
        /// <param name="host">The actor host specifying information needed for the creation of the actor.</param>
        /// <returns>
        /// Asynchronously returns n instance of <see cref="ActorActivatorState" />. The <see cref="ActorActivatorState" /> 
        /// instance will be provided to <see cref="DeleteAsync" /> when the actor is ready for deletion.
        /// </returns>
        /// <remarks>
        /// Implementations should not interact with lifecycle callback methods on the <see cref="Actor" /> type.
        /// These methods will be called by the runtime.
        /// </remarks>
        public abstract Task<ActorActivatorState> CreateAsync(ActorHost host);

        /// <summary>
        /// Deletes the actor instance and cleans up all associated resources.
        /// </summary>
        /// <param name="state">
        /// The <see cref="ActorActivatorState" /> instance that was created during creation of the actor.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous completion of the operation.
        /// </returns>
        /// <remarks>
        /// Implementations should not interact with lifecycle callback methods on the <see cref="Actor" /> type.
        /// These methods will be called by the runtime.
        /// </remarks>
        public abstract Task DeleteAsync(ActorActivatorState state);
    }
}
