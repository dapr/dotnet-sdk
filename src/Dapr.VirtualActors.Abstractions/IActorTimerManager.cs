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

namespace Dapr.VirtualActors;

/// <summary>
/// Manages timer and reminder operations for actors.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation communicates with the Dapr sidecar via gRPC.
/// Custom implementations can be used for testing or for adding behaviors like
/// scheduling policies or reminder batching.
/// </para>
/// </remarks>
public interface IActorTimerManager
{
    /// <summary>
    /// Registers a timer for the specified actor.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="timerName">The unique name of the timer.</param>
    /// <param name="callbackMethodName">The method to invoke when the timer fires.</param>
    /// <param name="callbackData">Optional data to pass to the callback.</param>
    /// <param name="dueTime">The delay before the first invocation.</param>
    /// <param name="period">The interval between subsequent invocations.</param>
    /// <param name="ttl">Optional time-to-live for the timer.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RegisterTimerAsync(
        string actorType,
        VirtualActorId actorId,
        string timerName,
        string callbackMethodName,
        byte[]? callbackData,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a timer.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="timerName">The name of the timer to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UnregisterTimerAsync(
        string actorType,
        VirtualActorId actorId,
        string timerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a reminder for the specified actor.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="reminderName">The unique name of the reminder.</param>
    /// <param name="data">Optional data to pass when the reminder fires.</param>
    /// <param name="dueTime">The delay before the first invocation.</param>
    /// <param name="period">The interval between subsequent invocations.</param>
    /// <param name="ttl">Optional time-to-live for the reminder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RegisterReminderAsync(
        string actorType,
        VirtualActorId actorId,
        string reminderName,
        byte[]? data,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a reminder.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="reminderName">The name of the reminder to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UnregisterReminderAsync(
        string actorType,
        VirtualActorId actorId,
        string reminderName,
        CancellationToken cancellationToken = default);
}
