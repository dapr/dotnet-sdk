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
    /// </remarks>
    public abstract class Actor
    {
        private const string TraceType = "Actor";
        private readonly string traceId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Actor"/> class.
        /// </summary>
        /// <param name="actorService">The <see cref="ActorService"/> that will host this actor instance.</param>
        /// <param name="actorId">Id for the actor.</param>
        protected Actor(ActorService actorService, ActorId actorId)
        {
            this.Id = actorId;
            this.traceId = this.Id.GetTraceId();
            this.IsDirty = false;
            this.ActorService = actorService;
            this.StateManager = new ActorStateManager(this);
        }

        /// <summary>
        /// Gets the identity of this actor.
        /// </summary>
        /// <value>The <see cref="ActorId"/> for the actor.</value>
        public ActorId Id { get; }

        /// <summary>
        /// Gets the host ActorService of this actor within the Actor runtime.
        /// </summary>
        /// <value>The <see cref="ActorService"/> for the actor.</value>
        public ActorService ActorService { get; }

        internal ActorTrace TraceSource => ActorTrace.Instance;

        internal bool IsDirty { get; private set; }

        /// <summary>
        /// Gets the StateManager for the actor.
        /// </summary>
        protected IActorStateManager StateManager { get; }

        internal async Task OnActivateInternalAsync()
        {
            await this.ResetStateAsync();
            await this.OnActivateAsync();
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Activated");

            // Save any state modifications done in user overridden Activate method.
            await this.SaveStateAsync();
        }

        internal async Task OnDeactivateInternalAsync()
        {
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Deactivating ...");
            await this.ResetStateAsync();
            await this.OnDeactivateAsync();
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Deactivated");
        }

        internal Task OnPreActorMethodAsyncInternal(ActorMethodContext actorMethodContext)
        {
            return this.OnPreActorMethodAsync(actorMethodContext);
        }

        internal async Task OnPostActorMethodAsyncInternal(ActorMethodContext actorMethodContext)
        {
            await this.OnPostActorMethodAsync(actorMethodContext);
            await this.SaveStateAsync();
        }

        internal void OnInvokeFailed()
        {
            this.IsDirty = true;
        }

        internal Task ResetStateAsync()
        {
            return this.StateManager.ClearCacheAsync();
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
                await this.StateManager.SaveStateAsync();
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
