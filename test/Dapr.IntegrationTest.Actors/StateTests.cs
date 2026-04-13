// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.State;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify Dapr actor state management, including TTL support
/// and state isolation across different actor proxy instances.
/// </summary>
public sealed class StateTests
{
    /// <summary>
    /// Verifies that a state entry set with a TTL is automatically removed after the TTL elapses.
    /// </summary>
    [Fact]
    public async Task ActorCanSaveStateWithTTL()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));
        var resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        await Task.Delay(TimeSpan.FromSeconds(2.5), cts.Token);

        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.GetState("key"));

        await proxy.SetState("key", "new-value", null);
        resp = await proxy.GetState("key");
        Assert.Equal("new-value", resp);
    }

    /// <summary>
    /// Verifies that re-setting a state entry with a new TTL correctly resets the expiry timer.
    /// </summary>
    [Fact]
    public async Task ActorStateTTLOverridesExisting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.SetState("key", "value", TimeSpan.FromSeconds(4));
        var resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        // Reset TTL to 4 seconds; the old 2-second window is discarded.
        await proxy.SetState("key", "value", TimeSpan.FromSeconds(4));

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        await Task.Delay(TimeSpan.FromSeconds(2.5), cts.Token);
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.GetState("key"));
    }

    /// <summary>
    /// Verifies that a TTL can be removed by overwriting the entry without a TTL, and
    /// that subsequently re-adding a TTL causes expiry again.
    /// </summary>
    [Fact]
    public async Task ActorStateTTLRemoveTTL()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));
        // Overwrite with no TTL – the entry should survive.
        await proxy.SetState("key", "value", null);
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        var resp = await proxy.GetState("key");
        Assert.Equal("value", resp);

        // Now apply a TTL again and verify the entry expires.
        await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromSeconds(2.5), cts.Token);
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.GetState("key"));
    }

    /// <summary>
    /// Verifies that two proxies pointing at the same actor ID share state, and that
    /// TTL expiry is visible through both.
    /// </summary>
    [Fact]
    public async Task ActorStateBetweenProxies()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy1 = proxyFactory.CreateActorProxy<IStateActor>(actorId, "StateActor");
        var proxy2 = proxyFactory.CreateActorProxy<IStateActor>(actorId, "StateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy1, cts.Token);

        await proxy1.SetState("key", "value", TimeSpan.FromSeconds(2));
        Assert.Equal("value", await proxy1.GetState("key"));
        Assert.Equal("value", await proxy2.GetState("key"));

        await Task.Delay(TimeSpan.FromSeconds(2.5), cts.Token);
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy1.GetState("key"));
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy2.GetState("key"));
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-state-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors();

        return await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddActors(options =>
                {
                    options.Actors.RegisterActor<StateActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
