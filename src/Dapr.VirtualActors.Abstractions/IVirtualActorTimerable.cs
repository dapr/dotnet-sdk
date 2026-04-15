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
/// Defines a timer callback interface that actor implementations can implement
/// to receive timer-based notifications.
/// </summary>
public interface IVirtualActorTimerable
{
    /// <summary>
    /// Registers a timer to fire a callback method on this actor.
    /// </summary>
    /// <param name="timerName">The name of the timer. Must be unique within this actor instance.</param>
    /// <param name="callbackMethodName">The name of the method on this actor to invoke when the timer fires.</param>
    /// <param name="callbackData">Optional data to pass to the callback method.</param>
    /// <param name="dueTime">The time to wait before the first timer invocation.</param>
    /// <param name="period">The time interval between subsequent timer invocations.</param>
    /// <param name="ttl">Optional time-to-live for the timer. The timer will be automatically unregistered after this duration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RegisterTimerAsync(
        string timerName,
        string callbackMethodName,
        byte[]? callbackData,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a previously registered timer.
    /// </summary>
    /// <param name="timerName">The name of the timer to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UnregisterTimerAsync(string timerName, CancellationToken cancellationToken = default);
}
