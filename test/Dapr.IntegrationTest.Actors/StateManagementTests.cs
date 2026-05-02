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
/// Integration tests that validate the correctness of the Dapr actor state manager,
/// including in-memory caching behaviour, <c>GetOrAdd</c> / <c>AddOrUpdate</c> semantics,
/// <c>ContainsState</c>, <c>TryGet</c>, removal, and multi-key isolation.
/// These tests are designed to prove that the behaviour is correct, not merely that the
/// existing implementation does not throw.
/// </summary>
public sealed class StateManagementTests
{
    /// <summary>
    /// Verifies that a value written via <c>SetStateAsync</c> is immediately readable from the
    /// in-memory cache within the same actor activation, without a round-trip to the state store.
    /// </summary>
    [Fact]
    public async Task SetStateAsync_IsImmediatelyReadableFromCache()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(ActorId.CreateRandom(), "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var result = await proxy.SetAndGetWithinSameActivation("cache-key", "expected-value");

        Assert.Equal("expected-value", result);
    }

    /// <summary>
    /// Verifies that a value persisted in one actor method call is visible to a subsequent
    /// call on the same actor ID, confirming that state is auto-saved after each method.
    /// </summary>
    [Fact]
    public async Task SetStateAsync_IsPersistableAcrossMethodCalls()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        // First call — sets state (auto-saved by the runtime on method return).
        await proxy.SetAndGetWithinSameActivation("persist-key", "persisted-value");

        // Second call — reads via a different method, proving the state was persisted.
        var read = await proxy.Read("persist-key");
        Assert.Equal("persisted-value", read);
    }

    /// <summary>
    /// Verifies that a second <c>SetStateAsync</c> on the same key within one activation
    /// correctly replaces the first cached value.
    /// </summary>
    [Fact]
    public async Task SetStateAsync_OverwriteReplacesValueInCache()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(ActorId.CreateRandom(), "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var result = await proxy.OverwriteAndRead("overwrite-key", "first-value", "second-value");

        // Only the second write should survive.
        Assert.Equal("second-value", result);
    }

    /// <summary>
    /// Verifies that two independent state keys set in the same activation do not interfere
    /// with each other and that both values are correctly returned.
    /// </summary>
    [Fact]
    public async Task SetStateAsync_MultipleKeysAreIndependent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(ActorId.CreateRandom(), "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var results = await proxy.SetMultipleAndGetAll("key-a", "value-a", "key-b", "value-b");

        Assert.Equal(2, results.Length);
        Assert.Equal("value-a", results[0]);
        Assert.Equal("value-b", results[1]);
    }

    /// <summary>
    /// Verifies that <c>ContainsStateAsync</c> returns <see langword="true"/> after a key has
    /// been set, and that the check is satisfied from cache within the same activation.
    /// </summary>
    [Fact]
    public async Task ContainsStateAsync_ReturnsTrueForExistingKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        // Persist the key so it is visible on subsequent calls.
        await proxy.SetAndGetWithinSameActivation("exists-key", "exists-value");

        var exists = await proxy.ContainsKey("exists-key");
        Assert.True(exists);
    }

    /// <summary>
    /// Verifies that <c>ContainsStateAsync</c> returns <see langword="false"/> for a key that
    /// has never been set.
    /// </summary>
    [Fact]
    public async Task ContainsStateAsync_ReturnsFalseForAbsentKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(ActorId.CreateRandom(), "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var exists = await proxy.ContainsKey($"no-such-key-{Guid.NewGuid():N}");
        Assert.False(exists);
    }

    /// <summary>
    /// Verifies that removing a key makes it immediately invisible to <c>ContainsStateAsync</c>
    /// within the same actor activation — before the removal is flushed to the state store.
    /// </summary>
    [Fact]
    public async Task RemoveStateAsync_IsImmediatelyReflectedInContainsState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        // Persist a key first.
        await proxy.SetAndGetWithinSameActivation("remove-key", "remove-value");
        Assert.True(await proxy.ContainsKey("remove-key"));

        // Remove and immediately verify the cache reflects the removal.
        var result = await proxy.RemoveAndCheckExists("remove-key");
        Assert.False(result.Exists);

        // Also verify the removal is durable across activations.
        Assert.False(await proxy.ContainsKey("remove-key"));
    }

    /// <summary>
    /// Verifies that <c>TryGetStateAsync</c> returns <c>HasValue = false</c> for a key that
    /// has never been written.
    /// </summary>
    [Fact]
    public async Task TryGetStateAsync_ReturnsFalseForAbsentKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(ActorId.CreateRandom(), "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var result = await proxy.TryGet($"absent-{Guid.NewGuid():N}");
        Assert.False(result.Exists);
        Assert.Null(result.Value);
    }

    /// <summary>
    /// Verifies that <c>TryGetStateAsync</c> returns <c>HasValue = true</c> and the correct
    /// value for a key that has been written.
    /// </summary>
    [Fact]
    public async Task TryGetStateAsync_ReturnsTrueAndValueForExistingKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.SetAndGetWithinSameActivation("tryget-key", "tryget-value");

        var result = await proxy.TryGet("tryget-key");
        Assert.True(result.Exists);
        Assert.Equal("tryget-value", result.Value);
    }

    /// <summary>
    /// Verifies that <c>GetOrAddStateAsync</c> returns the existing value when the key is
    /// already present — the default value must <em>not</em> overwrite the stored value.
    /// </summary>
    [Fact]
    public async Task GetOrAddStateAsync_ReturnsExistingValueAndDoesNotOverwrite()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        // Persist a known value.
        await proxy.SetAndGetWithinSameActivation("getOrAdd-key", "original-value");

        // GetOrAdd with a different default — must return the existing value, not the default.
        var result = await proxy.GetOrAdd("getOrAdd-key", "should-not-be-used");
        Assert.Equal("original-value", result);

        // Confirm the value in the store has not changed.
        Assert.Equal("original-value", await proxy.Read("getOrAdd-key"));
    }

    /// <summary>
    /// Verifies that <c>GetOrAddStateAsync</c> stores and returns the default value when the
    /// key does not yet exist.
    /// </summary>
    [Fact]
    public async Task GetOrAddStateAsync_StoresDefaultWhenKeyIsAbsent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var key = $"getOrAdd-new-{Guid.NewGuid():N}";
        var result = await proxy.GetOrAdd(key, "default-value");
        Assert.Equal("default-value", result);

        // The default must also be durable.
        Assert.Equal("default-value", await proxy.Read(key));
    }

    /// <summary>
    /// Verifies that <c>AddOrUpdateStateAsync</c> stores <paramref name="addValue"/> when the
    /// key does not exist.
    /// </summary>
    [Fact]
    public async Task AddOrUpdateStateAsync_AddsValueWhenKeyIsAbsent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var key = $"addOrUpdate-new-{Guid.NewGuid():N}";
        var result = await proxy.AddOrUpdate(key, "add-value", "update-value");
        Assert.Equal("add-value", result);
    }

    /// <summary>
    /// Verifies that <c>AddOrUpdateStateAsync</c> replaces an existing value with the
    /// update-factory result when the key is already present.
    /// </summary>
    [Fact]
    public async Task AddOrUpdateStateAsync_UpdatesValueWhenKeyExists()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        // Write a value first.
        await proxy.SetAndGetWithinSameActivation("addOrUpdate-existing-key", "initial-value");

        // AddOrUpdate must invoke the update factory.
        var result = await proxy.AddOrUpdate("addOrUpdate-existing-key", "add-value", "updated-value");
        Assert.Equal("updated-value", result);

        // Confirm the new value is durable.
        Assert.Equal("updated-value", await proxy.Read("addOrUpdate-existing-key"));
    }

    /// <summary>
    /// Verifies that <c>TryAddStateAsync</c> succeeds (returns <see langword="true"/>) when the
    /// key does not exist, and fails (returns <see langword="false"/>) when the key is already
    /// present.
    /// </summary>
    [Fact]
    public async Task TryAddStateAsync_AddSucceedsForNewKeyAndFailsForExisting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var proxy = proxyFactory.CreateActorProxy<IAdvancedStateActor>(actorId, "AdvancedStateActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var key = $"tryAdd-{Guid.NewGuid():N}";

        // First attempt — key is absent, must succeed.
        var addedFirst = await proxy.TryAdd(key, "first-value");
        Assert.True(addedFirst);

        // Second attempt — key now exists, must fail.
        var addedSecond = await proxy.TryAdd(key, "second-value");
        Assert.False(addedSecond);

        // The original value must be preserved.
        Assert.Equal("first-value", await proxy.Read(key));
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<ActorTestContext> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-state-mgmt-components");

        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors();

        var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddActors(options =>
                {
                    options.Actors.RegisterActor<AdvancedStateActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
        return new ActorTestContext(environment, testApp);
    }
}
