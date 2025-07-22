// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Runtime;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Dapr.Actors.Client;
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
    private readonly string actorTypeName;

    /// <summary>
    /// The Logger
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Actor"/> class.
    /// </summary>
    /// <param name="host">The <see cref="ActorHost"/> that will host this actor instance.</param>
    protected Actor(ActorHost host)
    {
        this.Host = host;
        this.StateManager = new ActorStateManager(this);
        this.actorTypeName = this.Host.ActorTypeInfo.ActorTypeName;
        this.Logger = host.LoggerFactory.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the identity of this actor.
    /// </summary>
    /// <value>The <see cref="ActorId"/> for the actor.</value>
    public ActorId Id => Host.Id;

    /// <summary>
    /// Gets the host of this actor within the actor runtime.
    /// </summary>
    /// <value>The <see cref="ActorHost"/> for the actor.</value>
    public ActorHost Host { get; }

    /// <summary>
    /// Gets the <see cref="IActorProxyFactory" /> used to create proxy client instances for communicating
    /// with other actors.
    /// </summary>
    public IActorProxyFactory ProxyFactory => this.Host.ProxyFactory;

    /// <summary>
    /// Gets the StateManager for the actor.
    /// </summary>
    public IActorStateManager StateManager { get; protected set; }

    internal async Task OnActivateInternalAsync()
    {
        await this.ResetStateAsync();
        await this.OnActivateAsync();

        this.Logger.LogDebug("Activated");

        // Save any state modifications done in user overridden Activate method.
        await this.SaveStateAsync();
    }

    internal async Task OnDeactivateInternalAsync()
    {
        this.Logger.LogDebug("Deactivating ...");
        await this.ResetStateAsync();
        await this.OnDeactivateAsync();
        this.Logger.LogDebug("Deactivated");
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

    internal async Task OnActorMethodFailedInternalAsync(ActorMethodContext actorMethodContext, Exception e)
    {
        await this.OnActorMethodFailedAsync(actorMethodContext, e);
        // Exception has been thrown by user code, reset the state in state manager.
        await this.ResetStateAsync();
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
    /// Override this method for performing any action when invoking actor method has thrown an exception.
    /// This method is invoked by actor runtime when invoking actor method has thrown an exception.
    /// </summary>
    /// <param name="actorMethodContext">
    /// An <see cref="ActorMethodContext"/> describing the method that was invoked by actor runtime prior to this method.
    /// </param>
    /// <param name="e">Exception thrown by either actor method or by OnPreActorMethodAsync/OnPostActorMethodAsync overriden methods.</param>
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
    protected virtual Task OnActorMethodFailedAsync(ActorMethodContext actorMethodContext, Exception e)
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
        return await RegisterReminderAsync(new ActorReminderOptions
        {
            ActorTypeName = this.actorTypeName,
            Id = this.Id,
            ReminderName = reminderName,
            State = state,
            DueTime = dueTime,
            Period = period,
            Ttl = null
        });
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
    /// <param name="ttl">The time interval after which the reminder will expire.</param>
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
        TimeSpan period,
        TimeSpan ttl)
    {
        return await RegisterReminderAsync(new ActorReminderOptions
        {
            ActorTypeName = this.actorTypeName,
            Id = this.Id,
            ReminderName = reminderName,
            State = state,
            DueTime = dueTime,
            Period = period,
            Ttl = ttl
        });
    }
        
    /// <summary>
    /// Registers a reminder with the actor.
    /// </summary>
    /// <param name="reminderName">The name of the reminder to register. The name must be unique per actor.</param>
    /// <param name="state">User state passed to the reminder invocation.</param>
    /// <param name="dueTime">The amount of time to delay before invoking the reminder for the first time. Specify negative one (-1) milliseconds to disable invocation. Specify zero (0) to invoke the reminder immediately after registration.</param>
    /// <param name="period">
    /// The time interval between reminder invocations after the first invocation.
    /// </param>
    /// <param name="repetitions">The number of repetitions for which the reminder should be invoked.</param>
    /// <param name="ttl">The time interval after which the reminder will expire.</param>
    /// <returns>
    /// A task that represents the asynchronous registration operation. The result of the task provides information about the registered reminder and is used to unregister the reminder using UnregisterReminderAsync.
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
        TimeSpan period,
        int repetitions,
        TimeSpan ttl)
    {
        return await RegisterReminderAsync(new ActorReminderOptions
        {
            ActorTypeName = this.actorTypeName,
            Id = this.Id,
            ReminderName = reminderName,
            State = state,
            DueTime = dueTime,
            Period = period,
            Repetitions = repetitions,
            Ttl = ttl
        });
    }
        
    /// <summary>
    /// Registers a reminder with the actor.
    /// </summary>
    /// <param name="reminderName">The name of the reminder to register. The name must be unique per actor.</param>
    /// <param name="state">User state passed to the reminder invocation.</param>
    /// <param name="dueTime">The amount of time to delay before invoking the reminder for the first time. Specify negative one (-1) milliseconds to disable invocation. Specify zero (0) to invoke the reminder immediately after registration.</param>
    /// <param name="period">
    /// The time interval between reminder invocations after the first invocation.
    /// </param>
    /// <param name="repetitions">The number of repetitions for which the reminder should be invoked.</param>
    /// <returns>
    /// A task that represents the asynchronous registration operation. The result of the task provides information about the registered reminder and is used to unregister the reminder using UnregisterReminderAsync.
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
        TimeSpan period,
        int repetitions)
    {
        return await RegisterReminderAsync(new ActorReminderOptions
        {
            ActorTypeName = this.actorTypeName,
            Id = this.Id,
            ReminderName = reminderName,
            State = state,
            DueTime = dueTime,
            Period = period,
            Repetitions = repetitions
        });
    }

    /// <summary>
    /// Registers a reminder with the actor.
    /// </summary>
    /// <param name="options">A <see cref="ActorReminderOptions" /> containing the various settings for an <see cref="ActorReminder"/>.</param>
    /// <returns>
    /// A task that represents the asynchronous registration operation. The result of the task provides information about the registered reminder and is used to unregister the reminder using UnregisterReminderAsync />.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The class deriving from <see cref="Dapr.Actors.Runtime.Actor" /> must implement <see cref="Dapr.Actors.Runtime.IRemindable" /> to consume reminder invocations. Multiple reminders can be registered at any time, uniquely identified by <paramref name="options.ReminderName" />. Existing reminders can also be updated by calling this method again. Reminder invocations are synchronized both with other reminders and other actor method callbacks.
    /// </para>
    /// </remarks>
    internal async Task<IActorReminder> RegisterReminderAsync(ActorReminderOptions options)
    {
        var reminder = new ActorReminder(options);
        await this.Host.TimerManager.RegisterReminderAsync(reminder);
        return reminder;
    }

    /// <summary>
    /// Gets a reminder previously registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync(ActorReminderOptions)" />.
    /// </summary>
    /// <param name="reminderName">The name of the reminder to get.</param>
    /// <returns>
    /// Returns a task that represents the asynchronous get operation. The result of the task contains the reminder if it exists, otherwise null.
    /// </returns>
    protected async Task<IActorReminder> GetReminderAsync(string reminderName)
    {
        return await this.Host.TimerManager.GetReminderAsync(new ActorReminderToken(this.actorTypeName, this.Id, reminderName));
    }

    /// <summary>
    /// Unregisters a reminder previously registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync(ActorReminderOptions)" />.
    /// </summary>
    /// <param name="reminder">The actor reminder to unregister.</param>
    /// <returns>
    /// Returns a task that represents the asynchronous unregistration operation.
    /// </returns>
    protected Task UnregisterReminderAsync(IActorReminder reminder)
    {
        var token = reminder is ActorReminderToken ? (ActorReminderToken)reminder : new ActorReminderToken(this.actorTypeName, this.Id, reminder.Name);
        return this.Host.TimerManager.UnregisterReminderAsync(token);
    }

    /// <summary>
    /// Unregisters a reminder previously registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync(ActorReminderOptions)" />.
    /// </summary>
    /// <param name="reminderName">The actor reminder name to unregister.</param>
    /// <returns>
    /// Returns a task that represents the asynchronous unregistration operation.
    /// </returns>
    protected Task UnregisterReminderAsync(string reminderName)
    {
        var token = new ActorReminderToken(this.actorTypeName, this.Id, reminderName);
        return this.Host.TimerManager.UnregisterReminderAsync(token);
    }

    /// <summary>
    /// Registers a Timer for the actor. A timer name is autogenerated by the runtime to keep track of it.
    /// </summary>
    /// <param name="timerName">Timer Name. If a timer name is not provided, a timer is autogenerated.</param>
    /// <param name="callback">
    /// The name of the method to be called when the timer fires.
    /// It has one parameter: the state object passed to RegisterTimer.
    /// It returns a <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
    /// </param>
    /// <param name="callbackParams">An object containing information to be used by the callback method, or null.</param>
    /// <param name="dueTime">The amount of time to delay before the async callback is first invoked.
    /// Specify negative one (-1) milliseconds to prevent the timer from starting.
    /// Specify zero (0) to start the timer immediately.
    /// </param>
    /// <param name="period">
    /// The time interval between invocations of the async callback.
    /// Specify negative one (-1) milliseconds to disable periodic signaling.</param>
    /// <returns>Returns IActorTimer object.</returns>
    public async Task<ActorTimer> RegisterTimerAsync(
        string timerName,
        string callback,
        byte[] callbackParams,
        TimeSpan dueTime,
        TimeSpan period)
    {
        return await RegisterTimerAsync(new ActorTimerOptions
        {
            ActorTypeName = this.actorTypeName,
            Id = this.Id,
            TimerName = timerName,
            TimerCallback = callback,
            Data = callbackParams,
            DueTime = dueTime,
            Period = period,
            Ttl = null
        });
    }

    /// <summary>
    /// Registers a Timer for the actor. A timer name is autogenerated by the runtime to keep track of it.
    /// </summary>
    /// <param name="timerName">Timer Name. If a timer name is not provided, a timer is autogenerated.</param>
    /// <param name="callback">
    /// The name of the method to be called when the timer fires.
    /// It has one parameter: the state object passed to RegisterTimer.
    /// It returns a <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
    /// </param>
    /// <param name="callbackParams">An object containing information to be used by the callback method, or null.</param>
    /// <param name="dueTime">The amount of time to delay before the async callback is first invoked.
    /// Specify negative one (-1) milliseconds to prevent the timer from starting.
    /// Specify zero (0) to start the timer immediately.
    /// </param>
    /// <param name="period">
    /// The time interval between invocations of the async callback.
    /// Specify negative one (-1) milliseconds to disable periodic signaling.</param>
    /// <param name="ttl">The time interval after which a Timer will expire.</param>
    /// <returns>Returns IActorTimer object.</returns>
    public async Task<ActorTimer> RegisterTimerAsync(
        string timerName,
        string callback,
        byte[] callbackParams,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan ttl)
    {
        return await RegisterTimerAsync(new ActorTimerOptions
        {
            ActorTypeName = this.actorTypeName,
            Id = this.Id,
            TimerName = timerName,
            TimerCallback = callback,
            Data = callbackParams,
            DueTime = dueTime,
            Period = period,
            Ttl = ttl
        });
    }

    internal async Task<ActorTimer> RegisterTimerAsync(ActorTimerOptions options)
    {
        // Validate that the timer callback specified meets all the required criteria for a valid callback method
        this.ValidateTimerCallback(this.Host, options.TimerCallback);

        // create a timer name to register with Dapr runtime.
        if (string.IsNullOrEmpty(options.TimerName))
        {
            options.TimerName = $"{this.Id}_Timer_{Guid.NewGuid()}";
        }

        var actorTimer = new ActorTimer(options);
        await this.Host.TimerManager.RegisterTimerAsync(actorTimer);
        return actorTimer;
    }

    /// <summary>
    /// Unregisters a Timer previously set on this actor.
    /// </summary>
    /// <param name="timer">An IActorTimer representing timer that needs to be unregistered.</param>
    /// <returns>Task representing the Unregister timer operation.</returns>
    protected async Task UnregisterTimerAsync(ActorTimer timer)
    {
        await this.Host.TimerManager.UnregisterTimerAsync(timer);
    }

    /// <summary>
    /// Unregisters a Timer previously set on this actor.
    /// </summary>
    /// <param name="timerName">Name of timer to unregister.</param>
    /// <returns>Task representing the Unregister timer operation.</returns>
    protected async Task UnregisterTimerAsync(string timerName)
    {
        var token = new ActorTimerToken(this.actorTypeName, this.Id, timerName);
        await this.Host.TimerManager.UnregisterTimerAsync(token);
    }

    internal MethodInfo GetMethodInfoUsingReflection(Type actorType, string callback)
    {
        return actorType.GetMethod(callback, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
    }

    internal void ValidateTimerCallback(ActorHost host, string callback)
    {
        var actorTypeName = host.ActorTypeInfo.ActorTypeName;
        var actorType = host.ActorTypeInfo.ImplementationType;

        MethodInfo methodInfo;
        try
        {
            methodInfo = this.GetMethodInfoUsingReflection(actorType, callback);
        }
        catch (AmbiguousMatchException)
        {
            // GetMethod will throw an AmbiguousMatchException if more than one methods are found with the same name
            throw new ArgumentException($"Timer callback method: {callback} cannot be overloaded.");
        }

        // Check if the method exists
        if (methodInfo == null)
        {
            throw new ArgumentException($"Timer callback method: {callback} does not exist in the Actor class: {actorTypeName}");
        }

        // The timer callback can accept only 0 or 1 parameters
        var parameters = methodInfo.GetParameters();
        if (parameters.Length > 1)
        {
            throw new ArgumentException("Timer callback can accept only zero or one parameters");
        }

        // The timer callback should return only Task return type
        if (methodInfo.ReturnType != typeof(Task))
        {
            throw new ArgumentException("Timer callback can only return type Task");
        }
    }
}