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

using System.Threading.Tasks;

namespace Dapr.Actors.Runtime;

/// <summary>
/// Provides timer and reminder functionality to an actor instance.
/// </summary>
public abstract class ActorTimerManager
{
    /// <summary>
    /// Registers the provided reminder with the runtime.
    /// </summary>
    /// <param name="reminder">The <see cref="ActorReminder" /> to register.</param>
    /// <returns>A task which will complete when the operation completes.</returns>
    public abstract Task RegisterReminderAsync(ActorReminder reminder);

    /// <summary>
    /// Gets a reminder previously registered using
    /// </summary>
    /// <param name="reminder">The <see cref="ActorReminderToken" /> to unregister.</param>
    /// <returns>A task which will complete when the operation completes.</returns>
    public abstract Task<IActorReminder> GetReminderAsync(ActorReminderToken reminder);

    /// <summary>
    /// Unregisters the provided reminder with the runtime.
    /// </summary>
    /// <param name="reminder">The <see cref="ActorReminderToken" /> to unregister.</param>
    /// <returns>A task which will complete when the operation completes.</returns>
    public abstract Task UnregisterReminderAsync(ActorReminderToken reminder);

    /// <summary>
    /// Registers the provided timer with the runtime.
    /// </summary>
    /// <param name="timer">The <see cref="ActorTimer" /> to register.</param>
    /// <returns>A task which will complete when the operation completes.</returns>
    public abstract Task RegisterTimerAsync(ActorTimer timer);

    /// <summary>
    /// Unregisters the provided timer with the runtime.
    /// </summary>
    /// <param name="timer">The <see cref="ActorTimerToken" /> to unregister.</param>
    /// <returns>A task which will complete when the operation completes.</returns>
    public abstract Task UnregisterTimerAsync(ActorTimerToken timer);
}