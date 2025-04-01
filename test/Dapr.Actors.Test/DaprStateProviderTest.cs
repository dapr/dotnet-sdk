// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

namespace Dapr.Actors.Test;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Dapr.Actors.Communication;
using Dapr.Actors.Runtime;
using Moq;

/// <summary>
/// Contains tests for DaprStateProvider.
/// </summary>
public class DaprStateProviderTest
{
    [Fact]
    public async Task SaveStateAsync()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var provider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var token = new CancellationToken();

        var stateChangeList = new List<ActorStateChange>();
        stateChangeList.Add(
            new ActorStateChange("key1", typeof(string), "value1", StateChangeKind.Add, DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(2))));
        stateChangeList.Add(
            new ActorStateChange("key2", typeof(string), "value2", StateChangeKind.Add, null));

        string content = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((actorType, actorId, data, token) => content = data)
            .Returns(Task.FromResult(true));

        await provider.SaveStateAsync("actorType", "actorId", stateChangeList, token);
        Assert.Equal(
            "[{\"operation\":\"upsert\",\"request\":{\"key\":\"key1\",\"value\":\"value1\",\"metadata\":{\"ttlInSeconds\":\"2\"}}},{\"operation\":\"upsert\",\"request\":{\"key\":\"key2\",\"value\":\"value2\"}}]",
            content
        );
    }

    [Fact]
    public async Task ContainsStateAsync()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var provider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));
        Assert.False(await provider.ContainsStateAsync("actorType", "actorId", "key", token));

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value\"", null)));
        Assert.True(await provider.ContainsStateAsync("actorType", "actorId", "key", token));

        var ttl = DateTime.UtcNow.AddSeconds(1);
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value\"", ttl)));
        Assert.True(await provider.ContainsStateAsync("actorType", "actorId", "key", token));

        ttl = DateTime.UtcNow.AddSeconds(-1);
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value\"", ttl)));
        Assert.False(await provider.ContainsStateAsync("actorType", "actorId", "key", token));
    }

    [Fact]
    public async Task TryLoadStateAsync()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var provider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));
        var resp = await provider.TryLoadStateAsync<string>("actorType", "actorId", "key", token);
        Assert.False(resp.HasValue);

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value\"", null)));
        resp = await provider.TryLoadStateAsync<string>("actorType", "actorId", "key", token);
        Assert.True(resp.HasValue);
        Assert.Equal("value", resp.Value.Value);
        Assert.False(resp.Value.TTLExpireTime.HasValue);

        var ttl = DateTime.UtcNow.AddSeconds(1);
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value\"", ttl)));
        resp = await provider.TryLoadStateAsync<string>("actorType", "actorId", "key", token);
        Assert.True(resp.HasValue);
        Assert.Equal("value", resp.Value.Value);
        Assert.True(resp.Value.TTLExpireTime.HasValue);
        Assert.Equal(ttl, resp.Value.TTLExpireTime.Value);

        ttl = DateTime.UtcNow.AddSeconds(-1);
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value\"", ttl)));
        resp = await provider.TryLoadStateAsync<string>("actorType", "actorId", "key", token);
        Assert.False(resp.HasValue);
    }
}