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
/// Defines a reminder callback interface that actor implementations can implement
/// to receive durable reminder-based notifications.
/// </summary>
/// <remarks>
/// Unlike timers, reminders are durable — they persist across actor deactivations and
/// node failures. Reminders fire until explicitly unregistered.
/// </remarks>
public interface IVirtualActorRemindable
{
    /// <summary>
    /// Called when a previously registered reminder fires.
    /// </summary>
    /// <param name="reminderName">The name of the reminder that fired.</param>
    /// <param name="state">The state data that was supplied when the reminder was registered.</param>
    /// <param name="dueTime">The configured due time of the reminder.</param>
    /// <param name="period">The configured period of the reminder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReceiveReminderAsync(
        string reminderName,
        byte[]? state,
        TimeSpan dueTime,
        TimeSpan period,
        CancellationToken cancellationToken = default);
}
