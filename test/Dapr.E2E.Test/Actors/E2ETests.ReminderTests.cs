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
namespace Dapr.E2E.Test;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.E2E.Test.Actors.Reminders;
using Xunit;

public partial class E2ETests : IAsyncLifetime
{
    [Fact]
    public async Task ActorCanStartAndStopReminder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // Start reminder, to count up to 10
        await proxy.StartReminder(new StartReminderOptions(){ Total = 10, });

        State state; 
        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            state = await proxy.GetState();
            this.Output.WriteLine($"Got Count: {state.Count} IsReminderRunning: {state.IsReminderRunning}");
            if (!state.IsReminderRunning)
            {
                break;
            }
        }

        // Should count up to exactly 10
        Assert.Equal(10, state.Count);
    }

    [Fact]
    public async Task ActorCanStartAndStopAndGetReminder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // Get reminder before starting it, should return null.
        var reminder = await proxy.GetReminder();
        Assert.Equal("null", reminder);

        // Start reminder, to count up to 10
        await proxy.StartReminder(new StartReminderOptions(){ Total = 10, });

        State state = new State(); 
        var countGetReminder = 0;
        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            reminder = await proxy.GetReminder();
            Assert.NotNull(reminder);

            // If reminder is null then it means the reminder has been stopped.
            if (reminder != "null")
            {   
                countGetReminder++;
                var reminderJson = JsonSerializer.Deserialize<JsonElement>(reminder);
                var name = reminderJson.GetProperty("name").ToString();
                var period = reminderJson.GetProperty("period").ToString();
                var dueTime = reminderJson.GetProperty("dueTime").ToString();

                Assert.Equal("test-reminder", name);
                Assert.Equal(TimeSpan.FromMilliseconds(50).ToString(), period);
                Assert.Equal(TimeSpan.Zero.ToString(), dueTime);
            }

            state = await proxy.GetState();
            this.Output.WriteLine($"Got Count: {state.Count} IsReminderRunning: {state.IsReminderRunning}");
            if (!state.IsReminderRunning)
            {
                break;
            }
        }

        // Should count up to exactly 10
        Assert.Equal(10, state.Count);
        // Should be able to Get Reminder at least once.
        Assert.True(countGetReminder > 0);
    }        

    [Fact]
    public async Task ActorCanStartReminderWithRepetitions()
    {
        int repetitions = 5;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // Reminder that should fire 5 times (repetitions) at an interval of 1s
        await proxy.StartReminderWithRepetitions(repetitions);
        var start = DateTime.Now;
            
        await Task.Delay(TimeSpan.FromSeconds(7));

        var state = await proxy.GetState();

        // Make sure the reminder fired 5 times (repetitions)
        Assert.Equal(repetitions, state.Count);
            
        // Make sure the reminder has fired and that it didn't fire within the past second since it should have expired.
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Reminder may not have triggered.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1), 
            $"Reminder triggered too recently. {DateTime.Now} - {state.Timestamp}");
    }
        
    [Fact]
    public async Task ActorCanStartReminderWithTtlAndRepetitions()
    {
        int repetitions = 2;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // Reminder that should fire 2 times (repetitions) at an interval of 1s
        await proxy.StartReminderWithTtlAndRepetitions(TimeSpan.FromSeconds(5), repetitions);
        var start = DateTime.Now;
            
        await Task.Delay(TimeSpan.FromSeconds(5));
            
        var state = await proxy.GetState();

        // Make sure the reminder fired 2 times (repetitions) whereas the ttl was 5 seconds.
        Assert.Equal(repetitions, state.Count);
            
        // Make sure the reminder has fired and that it didn't fire within the past second since it should have expired.
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Reminder may not have triggered.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1), 
            $"Reminder triggered too recently. {DateTime.Now} - {state.Timestamp}");
    }

    [Fact]
    public async Task ActorCanStartReminderWithTtl()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // Reminder that should fire 3 times (at 0, 1, and 2 seconds)
        await proxy.StartReminderWithTtl(TimeSpan.FromSeconds(2));

        // Record the start time and wait for longer than the reminder should exist for.
        var start = DateTime.Now;
        await Task.Delay(TimeSpan.FromSeconds(5));

        var state = await proxy.GetState();

        // Make sure the reminder has fired and that it didn't fire within the past second since it should have expired.
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Reminder may not have triggered.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1), $"Reminder triggered too recently. {DateTime.Now} - {state.Timestamp}");
    }
}