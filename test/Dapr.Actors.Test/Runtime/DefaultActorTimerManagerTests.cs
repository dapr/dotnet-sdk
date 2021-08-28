// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

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
            var interactor = new Mock<IDaprInteractor>();
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

            var data = JObject.Parse(actualData);
            Assert.True(data.TryGetValue("period", out _));
            Assert.True(data.TryGetValue("dueTime", out _));
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
            var interactor = new Mock<IDaprInteractor>();
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

            var data = JObject.Parse(actualData);
            Assert.True(data.TryGetValue("period", out _));
            Assert.True(data.TryGetValue("dueTime", out _));
        }
    }
}
