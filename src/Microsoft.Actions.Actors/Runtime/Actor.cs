// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the base class for actors.
    /// </summary>
    /// <remarks>
    /// The base type for actors, that provides the common functionality
    /// for actors that derive from <see cref="Actor"/>.
    /// The state is preserved across actor garbage collections and fail-overs.
    /// The storage and retrieval of the state is provided by the actor state provider. See
    /// <see cref="IActorStateProvider"/> for more information.
    /// </remarks>
    public abstract class Actor
    {
        private const string TraceType = "Actor";
        private readonly string traceId;
        private IActorStateManager stateManager;        

        internal Actor(ActorId actorId)
        {
            this.Id = actorId;
            this.traceId = this.Id.GetStorageKey();
            this.IsDirty = false;
            this.stateManager = new ActionsActorStateManager();
            this.IsInitialized = false;
        }

        /// <summary>
        /// Gets the identity of this actor with the actor service.
        /// </summary>
        /// <value>The <see cref="ActorId"/> for the actor.</value>
        public ActorId Id { get; }        

        internal ActorTrace TraceSource => ActorTrace.Instance;

        internal bool IsDirty { get; set; }

        internal bool IsInitialized { get; set; }                

        internal async Task OnActivateInternalAsync()
        {
            await this.OnActivateAsync();
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Activated");
        }

        internal virtual async Task OnDeactivateInternalAsync()
        {
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Deactivating ...");
            await this.stateManager.ClearCacheAsync();
            await this.OnDeactivateAsync();
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Deactivated");
        }

        internal Task OnPreActorMethodAsyncInternal(ActorMethodContext actorMethodContext)
        {
            return this.OnPreActorMethodAsync(actorMethodContext);
        }

        internal Task OnPostActorMethodAsyncInternal(ActorMethodContext actorMethodContext)
        {
            return this.OnPostActorMethodAsync(actorMethodContext);
        }

        internal void OnInvokeFailedInternal()
        {
            this.IsDirty = true;
        }

        /// <summary>
        /// Called from ActorManager to save state implicitly.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        internal Task SaveStateAsyncInternal()
        {
            return this.SaveStateAsync();
        }

        internal Task ResetStateAsync()
        {
            return this.stateManager.ClearCacheAsync();
        }

        internal Task OnPostActivateAsync()
        {
            return this.SaveStateAsync();
        }

        /// <summary>
        /// Saves all the state changes (add/update/remove) that were made since last call to
        /// <see cref="Actor.SaveStateAsync"/>,
        /// to the actor state provider associated with the actor.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        protected async Task SaveStateAsync()
        {
            if (!this.IsDirty)
            {
                await this.stateManager.SaveStateAsync();
            }
        }

        /// <summary>
        /// Override this method to initialize the members, initialize state or register timers. This method is called right after the actor is activated
        /// and before any method call or reminders are dispatched on it.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding OnActivateAsync operation.</returns>
        protected virtual Task OnActivateAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        ///  Override this method to release any resources. This method is called when actor is deactivated (garbage collected by Actor Runtime).
        ///  Actor operations like state changes should not be called from this method.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding OnDeactivateAsync operation.</returns>
        protected virtual Task OnDeactivateAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override this method for performing any actions prior to an actor method is invoked.
        /// This method is invoked by actor runtime just before invoking an actor method.
        /// </summary>
        /// <param name="actorMethodContext">
        /// An <see cref="ActorMethodContext"/> describing the method that will be invoked by actor runtime after this method finishes.
        /// </param>
        /// <returns>
        /// Returns a <see cref="Task">Task</see> representing pre-actor-method operation.
        /// </returns>
        /// <remarks>
        /// This method is invoked by actor runtime prior to:
        /// <list type="bullet">
        /// <item><description>Invoking an actor interface method when a client request comes.</description></item>
        /// <item><description>Invoking a method when a reminder fires.</description></item>
        /// <item><description>Invoking a timer callback when timer fires.</description></item>
        /// </list>
        /// </remarks>
        protected virtual Task OnPreActorMethodAsync(ActorMethodContext actorMethodContext)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override this method for performing any actions after an actor method has finished execution.
        /// This method is invoked by actor runtime an actor method has finished execution.
        /// </summary>
        /// <param name="actorMethodContext">
        /// An <see cref="ActorMethodContext"/> describing the method that was invoked by actor runtime prior to this method.
        /// </param>
        /// <returns>
        /// Returns a <see cref="Task">Task</see> representing post-actor-method operation.
        /// </returns>
        /// /// <remarks>
        /// This method is invoked by actor runtime prior to:
        /// <list type="bullet">
        /// <item><description>Invoking an actor interface method when a client request comes.</description></item>
        /// <item><description>Invoking a method when a reminder fires.</description></item>
        /// <item><description>Invoking a timer callback when timer fires.</description></item>
        /// </list>
        /// </remarks>
        protected virtual Task OnPostActorMethodAsync(ActorMethodContext actorMethodContext)
        {
            return Task.CompletedTask;
        }
    }
}
