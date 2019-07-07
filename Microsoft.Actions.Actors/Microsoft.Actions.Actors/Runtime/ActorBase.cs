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
    /// <seealso cref="Actor"/>
    public abstract class ActorBase
    {
        private const string TraceType = "ActorBase";
        private readonly string traceId;

        internal ActorBase(ActorId actorId)
        {
            this.Id = actorId;
            this.traceId = this.Id.GetStorageKey();
            this.IsDirty = false;
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
            await this.OnDeactivateAsync();
            this.TraceSource.WriteInfoWithId(TraceType, this.traceId, "Deactivated");
        }

        internal void OnInvokeFailedInternal()
        {
            this.IsDirty = true;
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
    }
}
