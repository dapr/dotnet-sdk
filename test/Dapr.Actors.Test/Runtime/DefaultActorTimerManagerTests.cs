// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Dapr.Actors.Runtime
{
    public sealed class DefaultActorTimerManagerTests
    {
        /// <summary>
        /// When register reminder is called, interactor is called with correct data.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task RegisterReminderAsync_CallsInteractor_WithCorrectData()
        {
            var actorId = "123";
            var actorType = "abc";
            var interactor = new Mock<TestDaprInteractor>();
            var defaultActorTimerManager = new DefaultActorTimerManager(interactor.Object);
            var actorReminder = new ActorReminder(actorType, new ActorId(actorId), "remindername", new byte[] { }, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            var actualData = string.Empty;
            
            interactor
                .Setup(d => d.RegisterReminderAsync(actorType, actorId, "remindername", It.Is<string>(data => !string.IsNullOrEmpty(data)), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((actorType, actorID, reminderName, data, token) => {
                    actualData = data;
                })
                .Returns(Task.CompletedTask);

            await defaultActorTimerManager.RegisterReminderAsync(actorReminder);

            JsonElement json = JsonSerializer.Deserialize<dynamic>(actualData);

            var isPeriodSet = json.TryGetProperty("period", out var period);
            var isdDueTimeSet = json.TryGetProperty("dueTime", out var dueTime);
            
            Assert.True(isPeriodSet);
            Assert.True(isdDueTimeSet);
            
            Assert.Equal("0h1m0s0ms", period.GetString());
            Assert.Equal("0h1m0s0ms", dueTime.GetString());
        }

        /// <summary>
        /// When register reminder is called with repetition, interactor is called with correct data.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task RegisterReminderAsync_WithRepetition_CallsInteractor_WithCorrectData()
        {
            var actorId = "123";
            var actorType = "abc";
            var interactor = new Mock<TestDaprInteractor>();
            var defaultActorTimerManager = new DefaultActorTimerManager(interactor.Object);
            var actorReminder = new ActorReminder(actorType, new ActorId(actorId), "remindername", new byte[] { }, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), 10);
            var actualData = string.Empty;

            interactor
                .Setup(d => d.RegisterReminderAsync(actorType, actorId, "remindername", It.Is<string>(data => !string.IsNullOrEmpty(data)), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, string, CancellationToken>((actorType, actorID, reminderName, data, token) => {
                    actualData = data;
                })
                .Returns(Task.CompletedTask);

            await defaultActorTimerManager.RegisterReminderAsync(actorReminder);

            JsonElement json = JsonSerializer.Deserialize<dynamic>(actualData);

            var isPeriodSet = json.TryGetProperty("period", out var period);
            var isdDueTimeSet = json.TryGetProperty("dueTime", out var dueTime);
            
            Assert.True(isPeriodSet);
            Assert.True(isdDueTimeSet);
            
            Assert.Equal("R10/PT1M", period.GetString());
            Assert.Equal("0h1m0s0ms", dueTime.GetString());
        }
    }
}
