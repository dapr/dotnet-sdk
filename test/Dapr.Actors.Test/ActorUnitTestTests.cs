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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;
using Moq;
using Xunit;

namespace Dapr.Actors;

// Tests that test that we can test... hmmmm...
public class ActorUnitTestTests
{
    [Fact]
    public async Task CanTestStartingAndStoppingTimer()
    {
        var timers = new List<ActorTimer>();

        var timerManager = new Mock<ActorTimerManager>(MockBehavior.Strict);
        timerManager
            .Setup(tm => tm.RegisterTimerAsync(It.IsAny<ActorTimer>()))
            .Callback<ActorTimer>(timer => timers.Add(timer))
            .Returns(Task.CompletedTask);
        timerManager
            .Setup(tm => tm.UnregisterTimerAsync(It.IsAny<ActorTimerToken>()))
            .Callback<ActorTimerToken>(timer => timers.RemoveAll(t => t.Name == timer.Name))
            .Returns(Task.CompletedTask);

        var host = ActorHost.CreateForTest<CoolTestActor>(new ActorTestOptions(){ TimerManager = timerManager.Object, });
        var actor = new CoolTestActor(host);

        // Start the timer
        var message = new Message()
        { 
            Text = "Remind me to tape the hockey game tonite.",
        };
        await actor.StartTimerAsync(message);
            
        var timer = Assert.Single(timers);
        Assert.Equal("record", timer.Name);
        Assert.Equal(TimeSpan.FromSeconds(5), timer.Period);
        Assert.Equal(TimeSpan.Zero, timer.DueTime);
            
        var state = JsonSerializer.Deserialize<Message>(timer.Data);
        Assert.Equal(message.Text, state.Text);

        // Simulate invoking the callback
        for (var i = 0; i < 10; i++)
        {
            await actor.Tick(timer.Data);
        }

        // Stop the timer
        await actor.StopTimerAsync();
        Assert.Empty(timers);
    }

    [Fact]
    public async Task CanTestStartingAndStoppingReminder()
    {
        var reminders = new List<ActorReminder>();
        IActorReminder getReminder = null;

        var timerManager = new Mock<ActorTimerManager>(MockBehavior.Strict);
        timerManager
            .Setup(tm => tm.RegisterReminderAsync(It.IsAny<ActorReminder>()))
            .Callback<ActorReminder>(reminder => reminders.Add(reminder))
            .Returns(Task.CompletedTask);
        timerManager
            .Setup(tm => tm.UnregisterReminderAsync(It.IsAny<ActorReminderToken>()))
            .Callback<ActorReminderToken>(reminder => reminders.RemoveAll(t => t.Name == reminder.Name))
            .Returns(Task.CompletedTask);
        timerManager
            .Setup(tm => tm.GetReminderAsync(It.IsAny<ActorReminderToken>()))
            .Returns(() => Task.FromResult(getReminder));

        var host = ActorHost.CreateForTest<CoolTestActor>(new ActorTestOptions(){ TimerManager = timerManager.Object, });
        var actor = new CoolTestActor(host);

        // Start the reminder
        var message = new Message()
        { 
            Text = "Remind me to tape the hockey game tonite.",
        };
        await actor.StartReminderAsync(message);
            
        var reminder = Assert.Single(reminders);
        Assert.Equal("record", reminder.Name);
        Assert.Equal(TimeSpan.FromSeconds(5), reminder.Period);
        Assert.Equal(TimeSpan.Zero, reminder.DueTime);
            
        var state = JsonSerializer.Deserialize<Message>(reminder.State);
        Assert.Equal(message.Text, state.Text);

        // Simulate invoking the reminder interface
        for (var i = 0; i < 10; i++)
        {
            await actor.ReceiveReminderAsync(reminder.Name, reminder.State, reminder.DueTime, reminder.Period);
        }

        getReminder = reminder;
        var reminderFromGet = await actor.GetReminderAsync();
        Assert.Equal(reminder, reminderFromGet);

        // Stop the reminder
        await actor.StopReminderAsync();
        Assert.Empty(reminders);
    }

    [Fact]
    public async Task ReminderReturnsNullIfNotAvailable()
    {
        var timerManager = new Mock<ActorTimerManager>(MockBehavior.Strict);
        timerManager
            .Setup(tm => tm.GetReminderAsync(It.IsAny<ActorReminderToken>()))
            .Returns(() => Task.FromResult<IActorReminder>(null));
            
        var host = ActorHost.CreateForTest<CoolTestActor>(new ActorTestOptions() { TimerManager = timerManager.Object, });
        var actor = new CoolTestActor(host);
            
        //There is no starting reminder, so this should always return null
        var retrievedReminder = await actor.GetReminderAsync();
        Assert.Null(retrievedReminder);
    }

    public interface ICoolTestActor : IActor
    {
    }

    public class Message
    {
        public string Text { get; set; }
        public bool IsImportant { get; set; }
    }

    public class CoolTestActor : Actor, ICoolTestActor, IRemindable
    {
        public CoolTestActor(ActorHost host)
            : base(host)
        {
        }

        public async Task StartTimerAsync(Message message)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
            await this.RegisterTimerAsync("record", nameof(Tick), bytes, dueTime: TimeSpan.Zero, period: TimeSpan.FromSeconds(5));
        }

        public async Task StopTimerAsync()
        {
            await this.UnregisterTimerAsync("record");
        }

        public async Task StartReminderAsync(Message message)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
            await this.RegisterReminderAsync("record", bytes, dueTime: TimeSpan.Zero, period: TimeSpan.FromSeconds(5));
        }

        public async Task<IActorReminder> GetReminderAsync()
        {
            return await this.GetReminderAsync("record");
        }

        public async Task StopReminderAsync()
        {
            await this.UnregisterReminderAsync("record");
        }

        public Task Tick(byte[] data)
        {
            return Task.CompletedTask;
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            return Task.CompletedTask;
        }
    }
}