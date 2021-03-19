// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;

namespace Dapr.Actors.Runtime
{
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
}
