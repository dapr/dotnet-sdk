// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Microsoft.Extensions.Logging;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// The base class for all Dapr virtual actor implementations.
/// </summary>
/// <remarks>
/// <para>
/// Actors derive from this class and use the <see cref="StateManager"/> to persist state
/// and the <see cref="Host"/> to access runtime services. The actor lifecycle is managed
/// by the Dapr runtime through activation and deactivation callbacks.
/// </para>
/// <para>
/// Actor instances are constructed via DI. The <see cref="VirtualActorHost"/> parameter
/// is always supplied by the runtime:
/// <code>
/// public class MyActor(VirtualActorHost host) : VirtualActor(host), IMyActor { }
/// </code>
/// </para>
/// </remarks>
public abstract class VirtualActor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActor"/> class.
    /// </summary>
    /// <param name="host">The host providing runtime services for this actor instance.</param>
    protected VirtualActor(VirtualActorHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        Logger = host.LoggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Gets the host providing runtime services for this actor instance.
    /// </summary>
    public VirtualActorHost Host { get; }

    /// <summary>
    /// Gets the unique identity of this actor.
    /// </summary>
    public VirtualActorId Id => Host.Id;

    /// <summary>
    /// Gets the actor state manager for this actor instance.
    /// </summary>
    public IActorStateManager StateManager => Host.StateManager;

    /// <summary>
    /// Gets the proxy factory for communicating with other actors.
    /// </summary>
    public IVirtualActorProxyFactory ProxyFactory => Host.ProxyFactory;

    /// <summary>
    /// Gets the logger for this actor instance.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Called when the actor is activated. Override to perform initialization logic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous activation.</returns>
    protected internal virtual Task OnActivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Called when the actor is deactivated. Override to perform cleanup logic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous deactivation.</returns>
    protected internal virtual Task OnDeactivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Called before an actor method is invoked. Override to add pre-processing logic.
    /// </summary>
    /// <param name="context">Context about the method being invoked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected internal virtual Task OnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Called after an actor method is invoked. Override to add post-processing logic.
    /// </summary>
    /// <param name="context">Context about the method that was invoked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected internal virtual Task OnPostActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Registers a durable reminder on this actor via the Dapr runtime.
    /// </summary>
    /// <remarks>
    /// Actors that call this method should implement <see cref="IVirtualActorRemindable"/>
    /// to receive reminder callbacks. An analyzer will warn if this method is called
    /// without implementing that interface.
    /// </remarks>
    /// <param name="reminderName">The reminder name. Must be unique within this actor instance.</param>
    /// <param name="state">Optional state to pass to the reminder callback.</param>
    /// <param name="dueTime">How long to wait before the first reminder fires.</param>
    /// <param name="period">How frequently the reminder fires after the first firing.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous registration.</returns>
    protected Task RegisterReminderAsync(
        string reminderName,
        byte[]? state,
        TimeSpan dueTime,
        TimeSpan period,
        CancellationToken cancellationToken = default) =>
        Host.TimerManager.RegisterReminderAsync(
            Host.ActorType, Host.Id, reminderName, state, dueTime, period, cancellationToken: cancellationToken);

    /// <summary>
    /// Unregisters a previously registered reminder.
    /// </summary>
    /// <param name="reminderName">The name of the reminder to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected Task UnregisterReminderAsync(string reminderName, CancellationToken cancellationToken = default) =>
        Host.TimerManager.UnregisterReminderAsync(Host.ActorType, Host.Id, reminderName, cancellationToken);

    /// <summary>
    /// Registers a timer on this actor via the Dapr runtime.
    /// </summary>
    /// <remarks>
    /// The callback method must exist on this actor class. An analyzer will warn if the
    /// named callback method cannot be found.
    /// </remarks>
    /// <param name="timerName">The timer name. Must be unique within this actor instance.</param>
    /// <param name="callbackMethodName">The name of the method to invoke when the timer fires.</param>
    /// <param name="callbackData">Optional data to pass to the callback.</param>
    /// <param name="dueTime">How long to wait before the first timer fires.</param>
    /// <param name="period">How frequently the timer fires after the first firing.</param>
    /// <param name="ttl">Optional time-to-live. Timer is unregistered after this duration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous registration.</returns>
    protected Task RegisterTimerAsync(
        string timerName,
        string callbackMethodName,
        byte[]? callbackData,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) =>
        Host.TimerManager.RegisterTimerAsync(
            Host.ActorType, Host.Id, timerName, callbackMethodName,
            callbackData, dueTime, period, ttl, cancellationToken);

    /// <summary>
    /// Unregisters a previously registered timer.
    /// </summary>
    /// <param name="timerName">The name of the timer to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected Task UnregisterTimerAsync(string timerName, CancellationToken cancellationToken = default) =>
        Host.TimerManager.UnregisterTimerAsync(Host.ActorType, Host.Id, timerName, cancellationToken);
}
