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

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Timers;

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
        await this.RegisterTimerAsync("test-timer", nameof(Tick), bytes, dueTime: TimeSpan.Zero, period: TimeSpan.FromMilliseconds(100));

        await this.StateManager.SetStateAsync<State>("timer-state", new State(){ IsTimerRunning = true, });
    }

    public async Task StartTimerWithTtl(TimeSpan ttl)
    {
        var options = new StartTimerOptions()
        {
            Total = 100,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, this.Host.JsonSerializerOptions);
        await this.RegisterTimerAsync("test-timer-ttl", nameof(Tick), bytes, TimeSpan.Zero, TimeSpan.FromSeconds(1), ttl);
        await this.StateManager.SetStateAsync<State>("timer-state", new State() { IsTimerRunning = true, });
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
        state.Timestamp = DateTime.Now;
        await this.StateManager.SetStateAsync("timer-state", state);
    }
}