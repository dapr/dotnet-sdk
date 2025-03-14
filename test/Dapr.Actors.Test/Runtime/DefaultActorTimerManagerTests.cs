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

// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Dapr.Actors.Runtime;

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
        var actorReminder = new ActorReminder(actorType, new ActorId(actorId), "remindername", Array.Empty<byte>(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        var actualData = string.Empty;
            
        interactor
            .Setup(d => d.RegisterReminderAsync(actorType, actorId, "remindername", It.Is<string>(data => !string.IsNullOrEmpty(data)), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, CancellationToken>((innerType, innerId, reminderName, data, token) => {
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
        const string actorId = "123";
        const string actorType = "abc";
        var interactor = new Mock<TestDaprInteractor>();
        var defaultActorTimerManager = new DefaultActorTimerManager(interactor.Object);
        var actorReminder = new ActorReminder(actorType, new ActorId(actorId), "remindername", new byte[] { }, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), 10);
        var actualData = string.Empty;

        interactor
            .Setup(d => d.RegisterReminderAsync(actorType, actorId, "remindername", It.Is<string>(data => !string.IsNullOrEmpty(data)), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, CancellationToken>((innerType, innerActorId, reminderName, data, token) => {
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

    /// <summary>
    /// Get the GetReminder method is called without a registered reminder, it should return null.
    /// </summary>
    [Fact]
    public async Task GetReminderAsync_ReturnsNullWhenUnavailable()
    {
        const string actorId = "123";
        const string actorType = "abc";
        const string reminderName = "reminderName";
        var interactor = new Mock<TestDaprInteractor>();
        interactor
            .Setup(d => d.GetReminderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var defaultActorTimerManager = new DefaultActorTimerManager(interactor.Object);

        var reminderResult = await defaultActorTimerManager.GetReminderAsync(new ActorReminderToken(actorType, new ActorId(actorId), reminderName));
        Assert.Null(reminderResult);
    }
        
    [Fact]
    public async Task GetReminderAsync_ReturnsNullWhenDeserialziationFails()
    {
        const string actorId = "123";
        const string actorType = "abc";
        const string reminderName = "reminderName";
        var interactor = new Mock<TestDaprInteractor>();
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            
        interactor
            .Setup(d => d.GetReminderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var defaultActorTimerManager = new DefaultActorTimerManager(interactor.Object);

        var reminderResult = await defaultActorTimerManager.GetReminderAsync(new ActorReminderToken(actorType, new ActorId(actorId), reminderName));
        Assert.Null(reminderResult);
    }

    [Fact]
    public async Task GetReminderAsync_ReturnsResultWhenAvailable()
    {
        const string actorId = "123";
        const string actorType = "abc";
        const string reminderName = "reminderName";
        var interactor = new Mock<TestDaprInteractor>();
            
        //Create the reminder we'll return
        var state = Array.Empty<byte>();
        var dueTime = TimeSpan.FromMinutes(1);
        var period = TimeSpan.FromMinutes(1);
        var actorReminder = new ActorReminder(actorType, new ActorId(actorId), "remindername", state, dueTime, period, 10);
            
        //Serialize and create the response value
        var actorReminderInfo = new ReminderInfo(actorReminder.State, actorReminder.DueTime, actorReminder.Period,
            actorReminder.Repetitions, actorReminder.Ttl);
        var serializedActorReminderInfo = await actorReminderInfo.SerializeAsync();
        var reminderResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(serializedActorReminderInfo)
        };
            
        //Register the response
        interactor
            .Setup(d => d.GetReminderAsync(actorType, actorId, reminderName, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((type, id, name, token) => {
            })
            .Returns(Task.FromResult(reminderResponse));
        var defaultActorTimerManager = new DefaultActorTimerManager(interactor.Object);
            
        var reminderResult = await defaultActorTimerManager.GetReminderAsync(new ActorReminderToken(actorType, new ActorId(actorId), reminderName));
        Assert.NotNull(reminderResult);
            
        Assert.Equal(dueTime, reminderResult.DueTime);
        Assert.Equal(state, reminderResult.State);
        Assert.Equal(period, reminderResult.Period);
        Assert.Equal(reminderName, reminderResult.Name);
    }
}
