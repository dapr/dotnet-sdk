// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Timers
{
    public class TimerActor : Actor, ITimerActor
    {
        public TimerActor(ActorHost host)
            : base(host)
        {
        }

        public Task Ping()
        {
            return Task.CompletedTask;
        }

        public Task<State> GetState()
        {
            return this.StateManager.GetOrAddStateAsync<State>("timer-state", new State());
        }

        // Starts a timer that will fire N times before stopping itself
        public async Task StartTimer(StartTimerOptions options)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(options, this.Host.JsonSerializerOptions);
            await this.RegisterTimerAsync("test-timer", nameof(Tick), bytes, dueTime: TimeSpan.Zero, period: TimeSpan.FromMilliseconds(50));

            await this.StateManager.SetStateAsync<State>("timer-state", new State(){ IsTimerRunning = true, });
        }

        private async Task Tick(byte[] bytes)
        {
            var options = JsonSerializer.Deserialize<StartTimerOptions>(bytes, this.Host.JsonSerializerOptions);
            var state = await this.StateManager.GetStateAsync<State>("timer-state");

            if (++state.Count == options.Total)
            {
                await this.UnregisterTimerAsync("test-timer");
                state.IsTimerRunning = false;
            }

            await this.StateManager.SetStateAsync("timer-state", state);
        }
    }
}
