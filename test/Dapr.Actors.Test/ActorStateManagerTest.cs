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
/// Contains tests for ActorStateManager.
/// </summary>
public sealed class ActorStateManagerTest
{
    [Fact]
    public async Task SetGet()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        const string key1 = "key1";
        const string key2 = "key2";
        const string val1 = "value1";
        const string val2 = "value2";
        const string val3 = "value3";
        const string val4 = "value4";
        const string val5 = "value5";
        const string val6 = "value6";
        
        await mngr.AddStateAsync(key1, val1, cts.Token);
        await mngr.AddStateAsync(key2, val2, cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));

        await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.AddStateAsync(key1, val3, cts.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.AddStateAsync(key2, val4, cts.Token));

        await mngr.SetStateAsync(key1, val5, cts.Token);
        await mngr.SetStateAsync(key2, val6, cts.Token);
        Assert.Equal(val5, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val6, await mngr.GetStateAsync<string>(key2, cts.Token));
    }

    [Fact]
    public async Task StateWithTTL()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        const string key1 = "key1";
        const string key2 = "key2";
        const string val1 = "value1";
        const string val2 = "value2";
        
        await mngr.AddStateAsync(key1, val1, TimeSpan.FromSeconds(1), cts.Token);
        await mngr.AddStateAsync(key2, val2, TimeSpan.FromSeconds(1), cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));

        await Task.Delay(TimeSpan.FromSeconds(1.5), cts.Token);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>(key1, cts.Token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>(key2, cts.Token));

        // Should be able to add state again after expiry and should not expire.
        await mngr.AddStateAsync(key1, val1, cts.Token);
        await mngr.AddStateAsync(key2, val2, cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));
        await Task.Delay(TimeSpan.FromSeconds(1.5), cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));
    }

    [Fact]
    public async Task StateRemoveAddTTL()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        const string key1 = "key1";
        const string key2 = "key2";
        const string val1 = "value1";
        const string val2 = "value2";
        
        await mngr.AddStateAsync(key1, val1, TimeSpan.FromSeconds(1), cts.Token);
        await mngr.AddStateAsync(key2, val2, TimeSpan.FromSeconds(1), cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));

        await mngr.SetStateAsync(key1, val1, cts.Token);
        await mngr.SetStateAsync(key2, val2, cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));

        // TTL is removed so state should not expire.
        await Task.Delay(TimeSpan.FromSeconds(1.5), cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));

        // Adding TTL back should expire state.
        await mngr.SetStateAsync(key1, val1, TimeSpan.FromSeconds(1), cts.Token);
        await mngr.SetStateAsync(key2, val2, TimeSpan.FromSeconds(1), cts.Token);
        Assert.Equal(val1, await mngr.GetStateAsync<string>(key1, cts.Token));
        Assert.Equal(val2, await mngr.GetStateAsync<string>(key2, cts.Token));
        await Task.Delay(TimeSpan.FromSeconds(1.5), cts.Token);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>(key1, cts.Token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>(key2, cts.Token));
    }

    [Fact]
    public async Task ValidateStateExpirationAndExceptions()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Existing key which has an expiry time of 1 second - this is triggered on the call to `this.actor.Host.StateProvider.ContainsStateAsync` during `mngr.AddStateAsync`
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value1\"", DateTime.UtcNow.AddSeconds(1))));
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.AddStateAsync("key1", "value3", TimeSpan.FromSeconds(1), cts.Token)); //This is placed before the interactor runs as cache is checked first
        Assert.Equal("value3", await mngr.GetStateAsync<string>("key1", cts.Token)); //Validate against the cache value

        // No longer return the value from the state provider.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        // Key should be expired after 1 seconds.
        await Task.Delay(TimeSpan.FromSeconds(1.5), cts.Token);
        
        // While the key will be in the cache, it should no longer be valid as it expired after a second and we've delayed 1.5 seconds 
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key1", cts.Token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.RemoveStateAsync("key1", cts.Token));
        await mngr.AddStateAsync("key1", "value2", TimeSpan.FromSeconds(1), cts.Token);
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key1", cts.Token));
    }

    [Fact]
    public async Task RemoveState()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.RemoveStateAsync("key1", token));

        await mngr.AddStateAsync("key1", "value1", token);
        await mngr.AddStateAsync("key2", "value2", token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));

        await mngr.RemoveStateAsync("key1", token);
        await mngr.RemoveStateAsync("key2", token);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key1", token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key2", token));

        // Should be able to add state again after removal.
        await mngr.AddStateAsync("key1", "value1", TimeSpan.FromSeconds(1), token);
        await mngr.AddStateAsync("key2", "value2", TimeSpan.FromSeconds(1), token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));
    }

    // [Fact]
    // public async Task AddStateAsync_NoTTL()
    // {
    //     var interactor = new Mock<TestDaprInteractor>();
    //     var host = ActorHost.CreateForTest<TestActor>();
    //     host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
    //     var mngr = new ActorStateManager(new TestActor(host));
    //     var cts = new CancellationTokenSource();
    //  
    //     
    // }
}
