// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dapr.Actors.Runtime
{
    internal class DefaultActorTimerManager : ActorTimerManager
    {
        private readonly IDaprInteractor interactor;

        public DefaultActorTimerManager(IDaprInteractor interactor)
        {
            this.interactor = interactor;
        }

        public override async Task RegisterReminderAsync(ActorReminder reminder)
        {
            if (reminder == null)
            {
                throw new ArgumentNullException(nameof(reminder));
            }

            var serialized = await SerializeReminderAsync(reminder);
            await this.interactor.RegisterReminderAsync(reminder.ActorType, reminder.ActorId.ToString(), reminder.Name, serialized);
        }

        public override async Task UnregisterReminderAsync(ActorReminderToken reminder)
        {
            if (reminder == null)
            {
                throw new ArgumentNullException(nameof(reminder));
            }
            
            await this.interactor.UnregisterReminderAsync(reminder.ActorType, reminder.ActorId.ToString(), reminder.Name);
        }

        public override async Task RegisterTimerAsync(ActorTimer timer)
        {
            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            #pragma warning disable 0618
            var timerInfo = new TimerInfo(timer.TimerCallback, timer.Data, timer.DueTime, timer.Period);
            #pragma warning restore 0618
            var data = JsonSerializer.Serialize(timerInfo);
            await this.interactor.RegisterTimerAsync(timer.ActorType, timer.ActorId.ToString(), timer.Name, data);
        }

        public override async Task UnregisterTimerAsync(ActorTimerToken timer)
        {
            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            await this.interactor.UnregisterTimerAsync(timer.ActorType, timer.ActorId.ToString(), timer.Name);
        }

        private async ValueTask<string> SerializeReminderAsync(ActorReminder reminder)
        {
            var info = new ReminderInfo(reminder.State, reminder.DueTime, reminder.Period);
            return await info.SerializeAsync();
        }
    }
}
