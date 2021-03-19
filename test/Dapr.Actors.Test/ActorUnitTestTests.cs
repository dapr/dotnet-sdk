// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dapr.Actors
{
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
        public async Task CanTestStartingAndStoppinReminder()
        {
            var reminders = new List<ActorReminder>();

            var timerManager = new Mock<ActorTimerManager>(MockBehavior.Strict);
            timerManager
                .Setup(tm => tm.RegisterReminderAsync(It.IsAny<ActorReminder>()))
                .Callback<ActorReminder>(reminder => reminders.Add(reminder))
                .Returns(Task.CompletedTask);
            timerManager
                .Setup(tm => tm.UnregisterReminderAsync(It.IsAny<ActorReminderToken>()))
                .Callback<ActorReminderToken>(reminder => reminders.RemoveAll(t => t.Name == reminder.Name))
                .Returns(Task.CompletedTask);

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

            // Stop the reminder
            await actor.StopReminderAsync();
            Assert.Empty(reminders);
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
}
