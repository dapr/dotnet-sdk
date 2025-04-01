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

namespace Dapr.E2E.Test.Actors.Reminders;

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

    public async Task<String> GetReminder(){
        var reminder = await this.GetReminderAsync("test-reminder");
        var reminderString = JsonSerializer.Serialize(reminder, this.Host.JsonSerializerOptions);
        return reminderString;
    }

    public async Task StartReminderWithTtl(TimeSpan ttl)
    {
        var options = new StartReminderOptions()
        {
            Total = 100,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, this.Host.JsonSerializerOptions);
        await this.RegisterReminderAsync("test-reminder-ttl", bytes, dueTime: TimeSpan.Zero, period: TimeSpan.FromSeconds(1), ttl: ttl);
        await this.StateManager.SetStateAsync<State>("reminder-state", new State() { IsReminderRunning = true, });
    }
        
    public async Task StartReminderWithRepetitions(int repetitions)
    {
        var options = new StartReminderOptions()
        {
            Total = 100,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, this.Host.JsonSerializerOptions);
        await this.RegisterReminderAsync("test-reminder-repetition", bytes, dueTime: TimeSpan.Zero, 
            period: TimeSpan.FromSeconds(1), repetitions: repetitions);
        await this.StateManager.SetStateAsync<State>("reminder-state", new State() 
            { IsReminderRunning = true, });
    }
        
    public async Task StartReminderWithTtlAndRepetitions(TimeSpan ttl, int repetitions)
    {
        var options = new StartReminderOptions()
        {
            Total = 100,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, this.Host.JsonSerializerOptions);
        await this.RegisterReminderAsync("test-reminder-ttl-repetition", bytes, dueTime: TimeSpan.Zero, 
            period: TimeSpan.FromSeconds(1), repetitions: repetitions, ttl: ttl);
        await this.StateManager.SetStateAsync<State>("reminder-state", new State() 
            { IsReminderRunning = true, });
    }

    public async Task ReceiveReminderAsync(string reminderName, byte[] bytes, TimeSpan dueTime, TimeSpan period)
    {
        if (!reminderName.StartsWith("test-reminder"))
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
        state.Timestamp = DateTime.Now;
        await this.StateManager.SetStateAsync("reminder-state", state);
    }
}