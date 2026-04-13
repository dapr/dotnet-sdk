// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

namespace Dapr.IntegrationTest.Actors.Timers;

/// <summary>
/// Implementation of <see cref="ITimerActor"/> that manages Dapr timers and tracks
/// invocation counts in actor state.
/// </summary>
public class TimerActor(ActorHost host) : Actor(host), ITimerActor
{
    private const string StateKey = "timer-state";

    /// <inheritdoc />
    public Task Ping() => Task.CompletedTask;

    /// <inheritdoc />
    public Task<TimerState> GetState() =>
        StateManager.GetOrAddStateAsync(StateKey, new TimerState());

    /// <inheritdoc />
    public async Task StartTimer(StartTimerOptions options)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, Host.JsonSerializerOptions);
        const string timerName = "test-timer";
        await RegisterTimerAsync(
            timerName,
            nameof(Tick),
            bytes,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMilliseconds(100));
        await StateManager.SetStateAsync(StateKey, new TimerState
        {
            IsTimerRunning = true,
            ActiveTimerName = timerName,
        });
    }

    /// <inheritdoc />
    public async Task StartTimerWithTtl(TimeSpan ttl)
    {
        var options = new StartTimerOptions { Total = 100 };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, Host.JsonSerializerOptions);
        const string timerName = "test-timer-ttl";
        await RegisterTimerAsync(
            timerName,
            nameof(Tick),
            bytes,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            ttl);
        await StateManager.SetStateAsync(StateKey, new TimerState
        {
            IsTimerRunning = true,
            ActiveTimerName = timerName,
        });
    }

    private async Task Tick(byte[] bytes)
    {
        var options = JsonSerializer.Deserialize<StartTimerOptions>(bytes, Host.JsonSerializerOptions)!;
        var state = await StateManager.GetStateAsync<TimerState>(StateKey);

        if (++state.Count == options.Total)
        {
            // Unregister the timer by the name tracked in state.
            if (!string.IsNullOrEmpty(state.ActiveTimerName))
                await UnregisterTimerAsync(state.ActiveTimerName);

            state.IsTimerRunning = false;
        }

        state.Timestamp = DateTime.Now;
        await StateManager.SetStateAsync(StateKey, state);
    }
}
