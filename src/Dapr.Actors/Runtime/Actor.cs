// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

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
        private readonly string traceId;
        private readonly string actorTypeName;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILogger logger { get; }

        /// <summary>
        /// Contains timers to be invoked.
        /// </summary>
        private readonly Dictionary<string, IActorTimer> timers = new Dictionary<string, IActorTimer>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Actor"/> class.
        /// </summary>
        /// <param name="actorService">The <see cref="ActorService"/> that will host this actor instance.</param>
        /// <param name="actorId">Id for the actor.</param>
        /// <param name="actorStateManager">The custom implementation of the StateManager.</param>
        protected Actor(ActorService actorService, ActorId actorId, IActorStateManager actorStateManager = default)
        {
            this.Id = actorId;
            this.traceId = this.Id.ToString();
            this.IsDirty = false;
            this.ActorService = actorService;
            this.StateManager = actorStateManager ?? new ActorStateManager(this);
            this.actorTypeName = this.ActorService.ActorTypeInfo.ActorTypeName;
            this.logger = actorService.LoggerFactory.CreateLogger(this.GetType());
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

        internal bool IsDirty { get; private set; }

        /// <summary>
        /// Gets the StateManager for the actor.
        /// </summary>
        protected IActorStateManager StateManager { get; }

        internal async Task OnActivateInternalAsync()
        {
            await this.ResetStateAsync();
            await this.OnActivateAsync();

            this.logger.LogDebug("Activated");

            // Save any state modifications done in user overridden Activate method.
            await this.SaveStateAsync();
        }

        internal async Task OnDeactivateInternalAsync()
        {
            this.logger.LogDebug("Deactivating ...");
            await this.ResetStateAsync();
            await this.OnDeactivateAsync();
            this.logger.LogDebug("Deactivated");
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

        internal Task OnInvokeFailedAsync()
        {
            // Exception has been thrown by user code, reset the state in state manager.
            return this.ResetStateAsync();
        }

        internal Task FireTimerAsync(string timerName)
        {
            var timer = this.timers[timerName];
            return timer.AsyncCallback.Invoke(timer.State);
        }

        internal Task ResetStateAsync()
        {
            // Exception has been thrown by user code, reset the state in state manager.
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
            await this.StateManager.SaveStateAsync();
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
        /// Override this method for performing any action prior to an actor method is invoked.
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
        /// Override this method for performing any action after an actor method has finished execution.
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

        /// <summary>
        /// Registers a reminder with the actor.
        /// </summary>
        /// <param name="reminderName">The name of the reminder to register. The name must be unique per actor.</param>
        /// <param name="state">User state passed to the reminder invocation.</param>
        /// <param name="dueTime">The amount of time to delay before invoking the reminder for the first time. Specify negative one (-1) milliseconds to disable invocation. Specify zero (0) to invoke the reminder immediately after registration.
        /// </param>
        /// <param name="period">
        /// The time interval between reminder invocations after the first invocation. Specify negative one (-1) milliseconds to disable periodic invocation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous registration operation. The result of the task provides information about the registered reminder and is used to unregister the reminder using UnregisterReminderAsync />.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The class deriving from <see cref="Dapr.Actors.Runtime.Actor" /> must implement <see cref="Dapr.Actors.Runtime.IRemindable" /> to consume reminder invocations. Multiple reminders can be registered at any time, uniquely identified by <paramref name="reminderName" />. Existing reminders can also be updated by calling this method again. Reminder invocations are synchronized both with other reminders and other actor method callbacks.
        /// </para>
        /// </remarks>
        protected async Task<IActorReminder> RegisterReminderAsync(
            string reminderName,
            byte[] state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var reminderInfo = new ReminderInfo(state, dueTime, period);
            var reminder = new ActorReminder(this.Id, reminderName, reminderInfo);
            var serializedReminderInfo = await reminderInfo.SerializeAsync();
            await ActorRuntime.DaprInteractor.RegisterReminderAsync(this.actorTypeName, this.Id.ToString(), reminderName, serializedReminderInfo);
            return reminder;
        }

        /// <summary>
        /// Unregisters a reminder previously registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync" />.
        /// </summary>
        /// <param name="reminder">The actor reminder to unregister.</param>
        /// <returns>
        /// Returns a task that represents the asynchronous unregistration operation.
        /// </returns>
        protected Task UnregisterReminderAsync(IActorReminder reminder)
        {
            return ActorRuntime.DaprInteractor.UnregisterReminderAsync(this.actorTypeName, this.Id.ToString(), reminder.Name);
        }

        /// <summary>
        /// Unregisters a reminder previously registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync" />.
        /// </summary>
        /// <param name="reminderName">The actor reminder name to unregister.</param>
        /// <returns>
        /// Returns a task that represents the asynchronous unregistration operation.
        /// </returns>
        protected Task UnregisterReminderAsync(string reminderName)
        {
            return ActorRuntime.DaprInteractor.UnregisterReminderAsync(this.actorTypeName, this.Id.ToString(), reminderName);
        }

        /// <summary>
        /// Registers a Timer for the actor. A timer name is autogenerated by the runtime to keep track of it.
        /// </summary>
        /// <param name="asyncCallback">
        /// A delegate that specifies a method to be called when the timer fires.
        /// It has one parameter: the state object passed to RegisterTimer.
        /// It returns a <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
        /// </param>
        /// <param name="state">An object containing information to be used by the callback method, or null.</param>
        /// <param name="dueTime">The amount of time to delay before the async callback is first invoked.
        /// Specify negative one (-1) milliseconds to prevent the timer from starting.
        /// Specify zero (0) to start the timer immediately.
        /// </param>
        /// <param name="period">
        /// The time interval between invocations of the async callback.
        /// Specify negative one (-1) milliseconds to disable periodic signaling.</param>
        /// <returns>Returns IActorTimer object.</returns>
        protected Task<IActorTimer> RegisterTimerAsync(
            Func<object, Task> asyncCallback,
            object state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return this.RegisterTimerAsync(null, asyncCallback, state, dueTime, period);
        }

        /// <summary>
        /// Registers a Timer for the actor. If a timer name is not provided, a timer is autogenerated.
        /// </summary>
        /// <param name="timerName">Timer Name. If a timer name is not provided, a timer is autogenerated.</param>
        /// <param name="asyncCallback">
        /// A delegate that specifies a method to be called when the timer fires.
        /// It has one parameter: the state object passed to RegisterTimer.
        /// It returns a <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
        /// </param>
        /// <param name="state">An object containing information to be used by the callback method, or null.</param>
        /// <param name="dueTime">The amount of time to delay before the async callback is first invoked.
        /// Specify negative one (-1) milliseconds to prevent the timer from starting.
        /// Specify zero (0) to start the timer immediately.
        /// </param>
        /// <param name="period">
        /// The time interval between invocations of the async callback.
        /// Specify negative one (-1) milliseconds to disable periodic signaling.</param>
        /// <returns>Returns IActorTimer object.</returns>
        protected async Task<IActorTimer> RegisterTimerAsync(
            string timerName,
            Func<object, Task> asyncCallback,
            object state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            // create a timer name to register with Dapr runtime.
            if (string.IsNullOrEmpty(timerName))
            {
                timerName = $"{this.Id}_Timer_{this.timers.Count + 1}";
            }

            var actorTimer = new ActorTimer(this, timerName, asyncCallback, state, dueTime, period);
            var serializedTimer = await actorTimer.SerializeAsync();
            await ActorRuntime.DaprInteractor.RegisterTimerAsync(this.actorTypeName, this.Id.ToString(), timerName, serializedTimer);

            this.timers[timerName] = actorTimer;
            return actorTimer;
        }

        /// <summary>
        /// Unregisters a Timer previously set on this actor.
        /// </summary>
        /// <param name="timer">An IActorTimer representing timer that needs to be unregistered.</param>
        /// <returns>Task representing the Unregister timer operation.</returns>
        protected async Task UnregisterTimerAsync(IActorTimer timer)
        {
            await ActorRuntime.DaprInteractor.UnregisterTimerAsync(this.actorTypeName, this.Id.ToString(), timer.Name);
            if (this.timers.ContainsKey(timer.Name))
            {
                this.timers.Remove(timer.Name);
            }
        }

        /// <summary>
        /// Unregisters a Timer previously set on this actor.
        /// </summary>
        /// <param name="timerName">Name of timer to unregister.</param>
        /// <returns>Task representing the Unregister timer operation.</returns>
        protected async Task UnregisterTimerAsync(string timerName)
        {
            await ActorRuntime.DaprInteractor.UnregisterTimerAsync(this.actorTypeName, this.Id.ToString(), timerName);
            if (this.timers.ContainsKey(timerName))
            {
                this.timers.Remove(timerName);
            }
        }
    }
}
