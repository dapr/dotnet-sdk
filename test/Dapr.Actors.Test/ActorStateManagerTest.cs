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
using System.Linq;

/// <summary>
/// Contains tests for ActorStateManager.
/// </summary>
public class ActorStateManagerTest
{
    [Fact]
    public async Task SetGet()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("key1", "value1", token);
        await mngr.AddStateAsync("key2", "value2", token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));

        await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.AddStateAsync("key1", "value3", token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.AddStateAsync("key2", "value4", token));

        await mngr.SetStateAsync("key1", "value5", token);
        await mngr.SetStateAsync("key2", "value6", token);
        Assert.Equal("value5", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value6", await mngr.GetStateAsync<string>("key2", token));
    }

    [Fact]
    public async Task StateWithTTL()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("key1", "value1", TimeSpan.FromSeconds(1), token);
        await mngr.AddStateAsync("key2", "value2", TimeSpan.FromSeconds(1), token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));

        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key1", token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key2", token));

        // Should be able to add state again after expiry and should not expire.
        await mngr.AddStateAsync("key1", "value1", token);
        await mngr.AddStateAsync("key2", "value2", token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));
    }

    [Fact]
    public async Task StateRemoveAddTTL()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("key1", "value1", TimeSpan.FromSeconds(1), token);
        await mngr.AddStateAsync("key2", "value2", TimeSpan.FromSeconds(1), token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));

        await mngr.SetStateAsync("key1", "value1", token);
        await mngr.SetStateAsync("key2", "value2", token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));

        // TTL is removed so state should not expire.
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));

        // Adding TTL back should expire state.
        await mngr.SetStateAsync("key1", "value1", TimeSpan.FromSeconds(1), token);
        await mngr.SetStateAsync("key2", "value2", TimeSpan.FromSeconds(1), token);
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key2", token));
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key1", token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key2", token));
    }

    [Fact]
    public async Task StateDaprdExpireTime()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = new CancellationToken();

        // Existing key which has an expiry time.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"value1\"", DateTime.UtcNow.AddSeconds(1))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => mngr.AddStateAsync("key1", "value3", token));
        Assert.Equal("value1", await mngr.GetStateAsync<string>("key1", token));

        // No longer return the value from the state provider.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        // Key should be expired after 1 seconds.
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("key1", token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.RemoveStateAsync("key1", token));
        await mngr.AddStateAsync("key1", "value2", TimeSpan.FromSeconds(1), token);
        Assert.Equal("value2", await mngr.GetStateAsync<string>("key1", token));
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

    // ----- TryAddStateAsync -----

    [Fact]
    public async Task TryAddStateAsync_ReturnsTrueForNewKey()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        Assert.True(await mngr.TryAddStateAsync("k1", "v1", token));
        Assert.Equal("v1", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task TryAddStateAsync_ReturnsFalseForExistingKey()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "v1", token);

        Assert.False(await mngr.TryAddStateAsync("k1", "v2", token));
        // Original value preserved.
        Assert.Equal("v1", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task TryAddStateAsync_WithTTL_ReturnsTrueForNewKey()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        Assert.True(await mngr.TryAddStateAsync("k1", "v1", TimeSpan.FromSeconds(10), token));
        Assert.Equal("v1", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task TryAddStateAsync_AfterRemove_ReturnsTrueAndRestoresKey()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "old", token);
        await mngr.RemoveStateAsync("k1", token);

        // After remove the tracker marks it as Remove, so TryAdd should succeed again.
        Assert.True(await mngr.TryAddStateAsync("k1", "new", token));
        Assert.Equal("new", await mngr.GetStateAsync<string>("k1", token));
    }

    // ----- ContainsStateAsync -----

    [Fact]
    public async Task ContainsStateAsync_ReturnsTrueWhenKeyInCache()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "v1", token);
        Assert.True(await mngr.ContainsStateAsync("k1", token));
    }

    [Fact]
    public async Task ContainsStateAsync_ReturnsFalseAfterRemove()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // The store holds "stored" — a GetState causes it to enter the cache as ChangeKind.None.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"stored\"", null)));

        // Load the key into the tracker (ChangeKind.None).
        await mngr.GetStateAsync<string>("k1", token);

        // Now remove — this marks the tracker entry as ChangeKind.Remove.
        await mngr.RemoveStateAsync("k1", token);

        // Capture the number of store hits so far.
        var callsBefore = interactor.Invocations
            .Count(i => i.Method.Name == nameof(TestDaprInteractor.GetStateAsync));

        // ContainsStateAsync with Remove in cache must return false WITHOUT contacting the store.
        Assert.False(await mngr.ContainsStateAsync("k1", token));

        var callsAfter = interactor.Invocations
            .Count(i => i.Method.Name == nameof(TestDaprInteractor.GetStateAsync));
        Assert.Equal(callsBefore, callsAfter);
    }

    // ----- TryRemoveStateAsync -----

    [Fact]
    public async Task TryRemoveStateAsync_ReturnsFalseForAbsentKey()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        Assert.False(await mngr.TryRemoveStateAsync("missing", token));
    }

    [Fact]
    public async Task TryRemoveStateAsync_ReturnsTrueForExistingCachedKey()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "v1", token);
        Assert.True(await mngr.TryRemoveStateAsync("k1", token));
        Assert.False(await mngr.ContainsStateAsync("k1", token));
    }

    [Fact]
    public async Task TryRemoveStateAsync_ReturnsFalseWhenAlreadyMarkedRemove()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // The store holds "stored" — load it into cache as ChangeKind.None.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"stored\"", null)));

        await mngr.GetStateAsync<string>("k1", token);

        // First remove: changes ChangeKind to Remove.
        Assert.True(await mngr.TryRemoveStateAsync("k1", token));

        // Second remove: key is already marked Remove — should return false.
        Assert.False(await mngr.TryRemoveStateAsync("k1", token));
    }

    // ----- GetOrAddStateAsync -----

    [Fact]
    public async Task GetOrAddStateAsync_ReturnsExistingValueWithoutOverwrite()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "original", token);
        var result = await mngr.GetOrAddStateAsync("k1", "default", token);

        Assert.Equal("original", result);
        Assert.Equal("original", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task GetOrAddStateAsync_AddsAndReturnsDefaultWhenAbsent()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        var result = await mngr.GetOrAddStateAsync("k1", "default", token);

        Assert.Equal("default", result);
        Assert.Equal("default", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task GetOrAddStateAsync_WithTTL_PreservesExistingEntryWithoutApplyingTTL()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        // Add without TTL — entry will never expire.
        await mngr.AddStateAsync("k1", "original", token);

        // GetOrAdd with a very short TTL should NOT apply that TTL to the existing entry.
        var result = await mngr.GetOrAddStateAsync("k1", "default", TimeSpan.FromMilliseconds(1), token);
        Assert.Equal("original", result);

        // Wait past what would be the TTL and confirm the entry is still accessible.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal("original", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task GetOrAddStateAsync_WithTTL_AddsWithTTLWhenAbsent()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        var result = await mngr.GetOrAddStateAsync("k1", "default", TimeSpan.FromSeconds(1), token);
        Assert.Equal("default", result);

        // Should be present immediately.
        Assert.Equal("default", await mngr.GetStateAsync<string>("k1", token));
    }

    // ----- AddOrUpdateStateAsync -----

    [Fact]
    public async Task AddOrUpdateStateAsync_AddsWhenKeyAbsent()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        var result = await mngr.AddOrUpdateStateAsync("k1", "added", (k, old) => old + "_updated", token);

        Assert.Equal("added", result);
        Assert.Equal("added", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_UpdatesWhenKeyPresentInCache()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "original", token);
        var result = await mngr.AddOrUpdateStateAsync("k1", "added", (k, old) => old + "_updated", token);

        Assert.Equal("original_updated", result);
        Assert.Equal("original_updated", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_AddsValueWhenKeyMarkedForRemove()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // Load "stored" from store (ChangeKind.None) then mark it for remove (ChangeKind.Remove).
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"stored\"", null)));

        await mngr.GetStateAsync<string>("k1", token);
        await mngr.RemoveStateAsync("k1", token);

        // Key is marked Remove — should use addValue (not call the update factory).
        var result = await mngr.AddOrUpdateStateAsync("k1", "added", (k, old) => old + "_updated", token);
        Assert.Equal("added", result);
        Assert.Equal("added", await mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_PromotesNoneToUpdate()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // Pretend the store holds "stored" — TryGet loads it into cache with ChangeKind.None.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"stored\"", null)));

        // Prime the cache: TryGetStateAsync loads it with ChangeKind.None.
        await mngr.GetStateAsync<string>("k1", token);

        // Now AddOrUpdate; the key is in cache with ChangeKind.None so it should be promoted to Update.
        string capturedData = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, data, _) => capturedData = data)
            .Returns(Task.CompletedTask);

        await mngr.AddOrUpdateStateAsync("k1", "added", (k, old) => old + "_updated", token);
        await mngr.SaveStateAsync(token);

        Assert.NotNull(capturedData);
        Assert.Contains("upsert", capturedData);
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_WithTTL_AddsWhenKeyAbsent()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        var result = await mngr.AddOrUpdateStateAsync("k1", "added", (k, old) => old + "_updated", TimeSpan.FromSeconds(10), token);
        Assert.Equal("added", result);
        Assert.Equal("added", await mngr.GetStateAsync<string>("k1", token));
    }

    // ----- ClearCacheAsync -----

    [Fact]
    public async Task ClearCacheAsync_DiscardsUnpersistedWrites()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // Store returns empty by default.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        await mngr.AddStateAsync("k1", "v1", token);

        // Clear the in-memory cache before saving.
        await mngr.ClearCacheAsync(token);

        // After clear the store still returns empty, so the key should be absent.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("k1", token));
    }

    [Fact]
    public async Task ClearCacheAsync_AllowsRereadingFromStore()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // The store holds "persisted".
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"persisted\"", null)));

        // Write something different to cache but don't save.
        await mngr.SetStateAsync("k1", "in-memory", token);

        // Clear cache — the next read should re-fetch from the store.
        await mngr.ClearCacheAsync(token);
        Assert.Equal("persisted", await mngr.GetStateAsync<string>("k1", token));
    }

    // ----- SaveStateAsync correctness -----

    [Fact]
    public async Task SaveStateAsync_DoesNotCallStoreWhenNothingChanged()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        // Nothing added or changed — save should be a no-op.
        await mngr.SaveStateAsync(token);

        interactor.Verify(
            d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveStateAsync_SecondSaveIsNoOpAfterFirstSave()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mngr.AddStateAsync("k1", "v1", token);
        await mngr.SaveStateAsync(token);

        // First save should call the store exactly once.
        interactor.Verify(
            d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Second save with no additional changes must be a no-op.
        await mngr.SaveStateAsync(token);
        interactor.Verify(
            d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveStateAsync_RemoveEvictsEntryFromTracker()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // The store holds "stored" — load it into cache with ChangeKind.None.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"stored\"", null)));
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mngr.GetStateAsync<string>("k1", token);

        // Mark for removal.
        await mngr.RemoveStateAsync("k1", token);
        await mngr.SaveStateAsync(token);

        // After save the tracker evicts the key, so TryAdd with the store returning empty succeeds.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        Assert.True(await mngr.TryAddStateAsync("k1", "v2", token));
    }

    // ----- SetState with TTL on cached entry -----

    [Fact]
    public async Task SetStateAsync_WithTTL_UpdatesTTLOnCachedNoneEntry()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        // Load "stored" into cache with ChangeKind.None.
        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"stored\"", null)));
        await mngr.GetStateAsync<string>("k1", token);

        // Now set with a TTL — should promote to Update and set TTLExpireTime.
        string capturedData = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, data, _) => capturedData = data)
            .Returns(Task.CompletedTask);

        await mngr.SetStateAsync("k1", "updated", TimeSpan.FromSeconds(60), token);
        await mngr.SaveStateAsync(token);

        Assert.NotNull(capturedData);
        Assert.Contains("upsert", capturedData);
        Assert.Contains("ttlInSeconds", capturedData);
    }

    // ----- SetStateContext (reentrancy) -----

    [Fact]
    public async Task SetStateContext_IsolatesContextualTrackerFromDefaultTracker()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var actor = new TestActor(host);
        var mngr = new ActorStateManager(actor);
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        // Write to the default (non-contextual) tracker.
        await mngr.AddStateAsync("default-key", "default-value", token);

        // Switch to a contextual tracker (simulate reentrancy context).
        await ((IActorContextualState)mngr).SetStateContext("ctx1");

        // The contextual tracker is empty — default-key is not visible.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("default-key", token));

        // Write to the contextual tracker.
        await mngr.AddStateAsync("ctx-key", "ctx-value", token);

        // Clear context — revert to default tracker.
        await ((IActorContextualState)mngr).SetStateContext(null);

        // Default tracker still has default-key but not ctx-key.
        Assert.Equal("default-value", await mngr.GetStateAsync<string>("default-key", token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => mngr.GetStateAsync<string>("ctx-key", token));
    }

    [Fact]
    public async Task SetStateContext_TwoContextsHaveIndependentTrackers()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var host = ActorHost.CreateForTest<TestActor>();
        host.StateProvider = new DaprStateProvider(interactor.Object, new JsonSerializerOptions());
        var mngr = new ActorStateManager(new TestActor(host));
        var token = CancellationToken.None;

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        // Two Tasks represent two concurrent reentrant calls, each with their own context.
        string ctx1Result = null;
        string ctx2Result = null;
        bool ctx1Saw = false;

        var ct = TestContext.Current.CancellationToken;
        var t1 = Task.Run(async () =>
        {
            await ((IActorContextualState)mngr).SetStateContext("ctx1");
            await mngr.AddStateAsync("shared-key", "from-ctx1", ct);
            ctx1Result = await mngr.GetStateAsync<string>("shared-key", ct);
            // ctx2's value should NOT be visible here.
            ctx1Saw = await mngr.ContainsStateAsync("ctx2-only", ct);
        }, ct);

        var t2 = Task.Run(async () =>
        {
            await ((IActorContextualState)mngr).SetStateContext("ctx2");
            await mngr.AddStateAsync("shared-key", "from-ctx2", ct);
            await mngr.AddStateAsync("ctx2-only", "yes", ct);
            ctx2Result = await mngr.GetStateAsync<string>("shared-key", ct);
        }, ct);

        await Task.WhenAll(t1, t2);

        Assert.Equal("from-ctx1", ctx1Result);
        Assert.Equal("from-ctx2", ctx2Result);
        Assert.False(ctx1Saw);
    }
}
