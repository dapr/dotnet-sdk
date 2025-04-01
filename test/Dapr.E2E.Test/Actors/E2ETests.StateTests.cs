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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.E2E.Test.Actors.State;
using Xunit;

public partial class E2ETests : IAsyncLifetime
{
    [Fact]
    public async Task ActorCanSaveStateWithTTL()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));

        var resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        // Assert key no longer exists.
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.GetState("key"));

        // Can create key again
        await proxy.SetState("key", "new-value", null);
        resp = await proxy.GetState("key");
        Assert.Equal("new-value", resp);
    }

    [Fact]
    public async Task ActorStateTTLOverridesExisting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // TLL 4 seconds
        await proxy.SetState("key", "value", TimeSpan.FromSeconds(4));

        var resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        // TLL 2 seconds
        await Task.Delay(TimeSpan.FromSeconds(2));
        resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        // TLL 4 seconds
        await proxy.SetState("key", "value", TimeSpan.FromSeconds(4));

        // TLL 2 seconds
        await Task.Delay(TimeSpan.FromSeconds(2));
        resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        // TLL 0 seconds
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        // Assert key no longer exists.
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.GetState("key"));
    }

    [Fact]
    public async Task ActorStateTTLRemoveTTL()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        // Can remove TTL and then add again
        await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));
        await proxy.SetState("key", "value", null);
        await Task.Delay(TimeSpan.FromSeconds(2));
        var resp = await proxy.GetState("key");
        Assert.Equal("value", resp);
        await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.GetState("key"));
    }

    [Fact]
    public async Task ActorStateBetweenProxies()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var actorId = ActorId.CreateRandom();
        var proxy1 = this.ProxyFactory.CreateActorProxy<IStateActor>(actorId, "StateActor");
        var proxy2 = this.ProxyFactory.CreateActorProxy<IStateActor>(actorId, "StateActor");

        await WaitForActorRuntimeAsync(proxy1, cts.Token);

        await proxy1.SetState("key", "value", TimeSpan.FromSeconds(2));
        var resp = await proxy1.GetState("key");
        Assert.Equal("value", resp);
        resp = await proxy2.GetState("key");
        Assert.Equal("value", resp);

        await Task.Delay(TimeSpan.FromSeconds(2.5));
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy1.GetState("key"));
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy2.GetState("key"));
    }
}