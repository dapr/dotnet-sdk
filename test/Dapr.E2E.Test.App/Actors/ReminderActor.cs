// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Reminders
{
    public class ReminderActor : Actor, IReminderActor, IRemindable
    {
        public ReminderActor(ActorHost host)
            : base(host)
        {
        }

        public Task Ping()
        {
            return Task.CompletedTask;
        }

        public Task<State> GetState()
        {
            return this.StateManager.GetOrAddStateAsync<State>("reminder-state", new State());
        }

        // Starts a reminder that will fire N times before stopping itself
        public async Task StartReminder(StartReminderOptions options)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(options, this.Host.JsonSerializerOptions);
            await this.RegisterReminderAsync("test-reminder", bytes, dueTime: TimeSpan.Zero, period: TimeSpan.FromMilliseconds(50));

            await this.StateManager.SetStateAsync<State>("reminder-state", new State(){ IsReminderRunning = true, });
        }

        public async Task ReceiveReminderAsync(string reminderName, byte[] bytes, TimeSpan dueTime, TimeSpan period)
        {
            if (reminderName != "test-reminder")
            {
                return;
            }

            var options = JsonSerializer.Deserialize<StartReminderOptions>(bytes, this.Host.JsonSerializerOptions);
            var state = await this.StateManager.GetStateAsync<State>("reminder-state");

            if (++state.Count == options.Total)
            {
                await this.UnregisterReminderAsync("test-reminder");
                state.IsReminderRunning = false;
            }

            await this.StateManager.SetStateAsync("reminder-state", state);
        }
    }
}
