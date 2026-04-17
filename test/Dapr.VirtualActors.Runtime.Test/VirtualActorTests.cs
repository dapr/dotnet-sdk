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

using Dapr.VirtualActors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Dapr.VirtualActors.Runtime.Test;

// Test actor implementations
public interface IGreeterActor : IVirtualActor
{
    Task<string> GreetAsync(string name, CancellationToken ct = default);
}

public class GreeterActor(VirtualActorHost host) : VirtualActor(host), IGreeterActor
{
    public int ActivationCount { get; private set; }
    public int DeactivationCount { get; private set; }

    public Task<string> GreetAsync(string name, CancellationToken ct = default) =>
        Task.FromResult($"Hello, {name}!");

    // Expose lifecycle for test verification
    public Task ActivateForTest(CancellationToken ct = default) => OnActivateAsync(ct);
    public Task DeactivateForTest(CancellationToken ct = default) => OnDeactivateAsync(ct);

    protected internal override Task OnActivateAsync(CancellationToken cancellationToken = default)
    {
        ActivationCount++;
        return Task.CompletedTask;
    }

    protected internal override Task OnDeactivateAsync(CancellationToken cancellationToken = default)
    {
        DeactivationCount++;
        return Task.CompletedTask;
    }
}

public class VirtualActorTests
{
    [Fact]
    public void Constructor_WithValidHost_Succeeds()
    {
        var host = VirtualActorHost.CreateForTest<GreeterActor>();
        var actor = new GreeterActor(host);

        actor.Host.ShouldBe(host);
        actor.Id.ShouldBe(host.Id);
        actor.StateManager.ShouldNotBeNull();
        actor.ProxyFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHost_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new GreeterActor(null!));
    }

    [Fact]
    public async Task OnActivateAsync_IsCalled()
    {
        var host = VirtualActorHost.CreateForTest<GreeterActor>();
        var actor = new GreeterActor(host);

        await actor.ActivateForTest(TestContext.Current.CancellationToken);

        actor.ActivationCount.ShouldBe(1);
    }

    [Fact]
    public async Task OnDeactivateAsync_IsCalled()
    {
        var host = VirtualActorHost.CreateForTest<GreeterActor>();
        var actor = new GreeterActor(host);

        await actor.DeactivateForTest(TestContext.Current.CancellationToken);

        actor.DeactivationCount.ShouldBe(1);
    }

    [Fact]
    public async Task ActorMethod_ReturnsExpectedResult()
    {
        var host = VirtualActorHost.CreateForTest<GreeterActor>();
        var actor = new GreeterActor(host);

        var result = await actor.GreetAsync("World", TestContext.Current.CancellationToken);

        result.ShouldBe("Hello, World!");
    }
}

public class VirtualActorHostTests
{
    [Fact]
    public void CreateForTest_WithDefaults_CreatesValidHost()
    {
        var host = VirtualActorHost.CreateForTest<GreeterActor>();

        host.ActorType.ShouldBe("GreeterActor");
        host.Id.GetId().ShouldNotBeNullOrWhiteSpace();
        host.StateManager.ShouldNotBeNull();
        host.ProxyFactory.ShouldNotBeNull();
        host.LoggerFactory.ShouldNotBeNull();
    }

    [Fact]
    public void CreateForTest_WithCustomActorId_UsesIt()
    {
        var customId = new VirtualActorId("custom-id");
        var host = VirtualActorHost.CreateForTest<GreeterActor>(actorId: customId);

        host.Id.ShouldBe(customId);
    }

    [Fact]
    public void CreateForTest_WithMockStateManager_UsesIt()
    {
        var mockStateManager = new Mock<IActorStateManager>();
        var host = VirtualActorHost.CreateForTest<GreeterActor>(
            stateManager: mockStateManager.Object);

        host.StateManager.ShouldBe(mockStateManager.Object);
    }

    [Fact]
    public void CreateForTest_WithMockProxyFactory_UsesIt()
    {
        var mockProxyFactory = new Mock<IVirtualActorProxyFactory>();
        var host = VirtualActorHost.CreateForTest<GreeterActor>(
            proxyFactory: mockProxyFactory.Object);

        host.ProxyFactory.ShouldBe(mockProxyFactory.Object);
    }

    [Fact]
    public void Constructor_WithNullActorType_Throws()
    {
        Should.Throw<ArgumentException>(() => new VirtualActorHost(
            new VirtualActorId("1"),
            "",
            new Mock<IActorStateManager>().Object,
            new Mock<IVirtualActorProxyFactory>().Object,
            new Mock<IActorTimerManager>().Object,
            NullLoggerFactory.Instance));
    }

    [Fact]
    public void Constructor_WithNullStateManager_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new VirtualActorHost(
            new VirtualActorId("1"),
            "TestActor",
            null!,
            new Mock<IVirtualActorProxyFactory>().Object,
            new Mock<IActorTimerManager>().Object,
            NullLoggerFactory.Instance));
    }
}
