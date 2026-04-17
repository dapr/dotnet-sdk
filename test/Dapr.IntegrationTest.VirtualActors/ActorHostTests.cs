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

using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Dapr.IntegrationTest.VirtualActors;

/// <summary>
/// Integration tests for VirtualActorHost and actor lifecycle helpers.
/// Tests cover the test harness helpers (CreateForTest) so developers can easily
/// write unit tests for their own actor implementations.
/// </summary>
public class ActorHostTests
{
    // ---------------------------------------------------------------------------
    // Test actors
    // ---------------------------------------------------------------------------

    public interface ILifecycleActor : IVirtualActor
    {
        Task<int> GetActivationCountAsync(CancellationToken ct = default);
        Task<int> GetDeactivationCountAsync(CancellationToken ct = default);
        Task<string> EchoAsync(string message, CancellationToken ct = default);
    }

    public class LifecycleActor(VirtualActorHost host) : VirtualActor(host), ILifecycleActor
    {
        private int _activations;
        private int _deactivations;

        protected internal override Task OnActivateAsync(CancellationToken cancellationToken = default)
        {
            _activations++;
            return Task.CompletedTask;
        }

        protected internal override Task OnDeactivateAsync(CancellationToken cancellationToken = default)
        {
            _deactivations++;
            return Task.CompletedTask;
        }

        public Task<int> GetActivationCountAsync(CancellationToken ct = default) =>
            Task.FromResult(_activations);

        public Task<int> GetDeactivationCountAsync(CancellationToken ct = default) =>
            Task.FromResult(_deactivations);

        public Task<string> EchoAsync(string message, CancellationToken ct = default) =>
            Task.FromResult(message);
    }

    public interface IStatefulActor : IVirtualActor
    {
        Task StoreValueAsync(string key, string value, CancellationToken ct = default);
        Task<string?> GetValueAsync(string key, CancellationToken ct = default);
    }

    public class StatefulActor(VirtualActorHost host) : VirtualActor(host), IStatefulActor
    {
        private readonly Dictionary<string, string> _store = new();

        public Task StoreValueAsync(string key, string value, CancellationToken ct = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetValueAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);
    }

    // ---------------------------------------------------------------------------
    // VirtualActorHost.CreateForTest tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateForTest_DefaultId_IsNonEmpty()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        host.Id.GetId().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateForTest_ExplicitId_UsesProvidedId()
    {
        var id = new VirtualActorId("test-actor-42");
        var host = VirtualActorHost.CreateForTest<LifecycleActor>(actorId: id);
        host.Id.GetId().ShouldBe("test-actor-42");
    }

    [Fact]
    public void CreateForTest_ActorTypeName_IsClassName()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        host.ActorType.ShouldBe("LifecycleActor");
    }

    [Fact]
    public void CreateForTest_DefaultStateManager_IsNoOp()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        host.StateManager.ShouldBeOfType<NoOpActorStateManager>();
    }

    [Fact]
    public void CreateForTest_DefaultProxyFactory_IsNoOp()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        host.ProxyFactory.ShouldBeOfType<NoOpVirtualActorProxyFactory>();
    }

    [Fact]
    public void CreateForTest_DefaultLoggerFactory_IsNull()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        host.LoggerFactory.ShouldNotBeNull();
    }

    [Fact]
    public void CreateForTest_CustomLoggerFactory_UsesProvided()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>(
            loggerFactory: NullLoggerFactory.Instance);
        host.LoggerFactory.ShouldBe(NullLoggerFactory.Instance);
    }

    // ---------------------------------------------------------------------------
    // Actor activation/deactivation tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnActivateAsync_CalledDirectly_IncrementsCounter()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        var actor = new LifecycleActor(host);

        await actor.OnActivateAsync(TestContext.Current.CancellationToken);

        (await actor.GetActivationCountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task OnDeactivateAsync_CalledDirectly_IncrementsCounter()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        var actor = new LifecycleActor(host);

        await actor.OnDeactivateAsync(TestContext.Current.CancellationToken);

        (await actor.GetDeactivationCountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task ActorMethod_ReturnsExpectedValue()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        var actor = new LifecycleActor(host);

        var result = await actor.EchoAsync("hello", TestContext.Current.CancellationToken);
        result.ShouldBe("hello");
    }

    [Fact]
    public async Task ActorState_InMemory_PersistsWithinInstance()
    {
        var host = VirtualActorHost.CreateForTest<StatefulActor>();
        var actor = new StatefulActor(host);

        await actor.StoreValueAsync("key1", "value1", TestContext.Current.CancellationToken);
        var retrieved = await actor.GetValueAsync("key1", TestContext.Current.CancellationToken);

        retrieved.ShouldBe("value1");
    }

    [Fact]
    public async Task ActorState_MissingKey_ReturnsNull()
    {
        var host = VirtualActorHost.CreateForTest<StatefulActor>();
        var actor = new StatefulActor(host);

        var retrieved = await actor.GetValueAsync("nonexistent", TestContext.Current.CancellationToken);
        retrieved.ShouldBeNull();
    }

    // ---------------------------------------------------------------------------
    // VirtualActorHost public properties
    // ---------------------------------------------------------------------------

    [Fact]
    public void VirtualActor_IdProperty_DelegatesToHost()
    {
        var id = new VirtualActorId("my-id");
        var host = VirtualActorHost.CreateForTest<LifecycleActor>(actorId: id);
        var actor = new LifecycleActor(host);

        actor.Id.ShouldBe(id);
    }

    [Fact]
    public void VirtualActor_StateManagerProperty_DelegatesToHost()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        var actor = new LifecycleActor(host);

        actor.StateManager.ShouldBe(host.StateManager);
    }

    [Fact]
    public void VirtualActor_ProxyFactoryProperty_DelegatesToHost()
    {
        var host = VirtualActorHost.CreateForTest<LifecycleActor>();
        var actor = new LifecycleActor(host);

        actor.ProxyFactory.ShouldBe(host.ProxyFactory);
    }

    // ---------------------------------------------------------------------------
    // NoOpActorStateManager tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task NoOpStateManager_TryGetState_ReturnsNone()
    {
        var stateManager = new NoOpActorStateManager();

        var result = await stateManager.TryGetStateAsync<string>(
            "key", TestContext.Current.CancellationToken);

        result.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task NoOpStateManager_SetAndGet_ReturnsNone()
    {
        var stateManager = new NoOpActorStateManager();

        await stateManager.SetStateAsync("key", "value", TestContext.Current.CancellationToken);
        var result = await stateManager.TryGetStateAsync<string>(
            "key", TestContext.Current.CancellationToken);

        // NoOp doesn't actually store — it's a test double
        result.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task NoOpStateManager_RemoveState_DoesNotThrow()
    {
        var stateManager = new NoOpActorStateManager();

        await Should.NotThrowAsync(async () =>
            await stateManager.RemoveStateAsync("key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NoOpStateManager_ContainsState_ReturnsFalse()
    {
        var stateManager = new NoOpActorStateManager();

        var contains = await stateManager.ContainsStateAsync(
            "key", TestContext.Current.CancellationToken);
        contains.ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------
    // NoOpVirtualActorProxyFactory tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void NoOpProxyFactory_Create_ThrowsNotSupportedException()
    {
        var proxyFactory = new NoOpVirtualActorProxyFactory();
        var id = new VirtualActorId("test");

        Should.Throw<NotSupportedException>(() =>
            proxyFactory.CreateProxy<ILifecycleActor>(id, "LifecycleActor"));
    }
}
