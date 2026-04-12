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

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Reminders;

/// <summary>
/// Implementation of <see cref="IReminderActor"/> that manages Dapr reminders and tracks
/// invocation counts in actor state.
/// </summary>
public class ReminderActor : Actor, IReminderActor, IRemindable
{
    private const string StateKey = "reminder-state";

    /// <summary>
    /// Initializes a new instance of <see cref="ReminderActor"/>.
    /// </summary>
    /// <param name="host">The actor host provided by the Dapr runtime.</param>
    public ReminderActor(ActorHost host) : base(host)
    {
    }

    /// <inheritdoc />
    public Task Ping() => Task.CompletedTask;

    /// <inheritdoc />
    public Task<ReminderState> GetState() =>
        StateManager.GetOrAddStateAsync(StateKey, new ReminderState());

    /// <inheritdoc />
    public async Task<string> GetReminder()
    {
        var reminder = await GetReminderAsync("test-reminder");
        return JsonSerializer.Serialize(reminder, Host.JsonSerializerOptions);
    }

    /// <inheritdoc />
    public async Task StartReminder(StartReminderOptions options)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, Host.JsonSerializerOptions);
        await RegisterReminderAsync(
            "test-reminder", bytes,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMilliseconds(50));
        await StateManager.SetStateAsync(StateKey, new ReminderState { IsReminderRunning = true });
    }

    /// <inheritdoc />
    public async Task StartReminderWithTtl(TimeSpan ttl)
    {
        var options = new StartReminderOptions { Total = 100 };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, Host.JsonSerializerOptions);
        await RegisterReminderAsync(
            "test-reminder-ttl", bytes,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(1),
            ttl: ttl);
        await StateManager.SetStateAsync(StateKey, new ReminderState { IsReminderRunning = true });
    }

    /// <inheritdoc />
    public async Task StartReminderWithRepetitions(int repetitions)
    {
        var options = new StartReminderOptions { Total = 100 };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, Host.JsonSerializerOptions);
        await RegisterReminderAsync(
            "test-reminder-repetition", bytes,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(1),
            repetitions: repetitions);
        await StateManager.SetStateAsync(StateKey, new ReminderState { IsReminderRunning = true });
    }

    /// <inheritdoc />
    public async Task StartReminderWithTtlAndRepetitions(TimeSpan ttl, int repetitions)
    {
        var options = new StartReminderOptions { Total = 100 };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, Host.JsonSerializerOptions);
        await RegisterReminderAsync(
            "test-reminder-ttl-repetition", bytes,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(1),
            repetitions: repetitions,
            ttl: ttl);
        await StateManager.SetStateAsync(StateKey, new ReminderState { IsReminderRunning = true });
    }

    /// <inheritdoc />
    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        if (!reminderName.StartsWith("test-reminder", StringComparison.Ordinal))
            return;

        var options = JsonSerializer.Deserialize<StartReminderOptions>(state, Host.JsonSerializerOptions)!;
        var current = await StateManager.GetStateAsync<ReminderState>(StateKey);

        if (++current.Count == options.Total)
        {
            await UnregisterReminderAsync("test-reminder");
            current.IsReminderRunning = false;
        }

        current.Timestamp = DateTime.Now;
        await StateManager.SetStateAsync(StateKey, current);
    }
}
