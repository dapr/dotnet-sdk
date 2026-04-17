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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Dapr.IntegrationTest.VirtualActors;

/// <summary>
/// Integration tests for DI registration, options configuration, and actor registry plumbing.
/// These tests do not require a running Dapr sidecar — they validate the SDK infrastructure.
/// </summary>
public class DependencyInjectionTests
{
    // ---------------------------------------------------------------------------
    // Shared test actors
    // ---------------------------------------------------------------------------

    public interface ICounterActor : IVirtualActor
    {
        Task IncrementAsync(CancellationToken ct = default);
        Task<int> GetCountAsync(CancellationToken ct = default);
    }

    public class CounterActor(VirtualActorHost host) : VirtualActor(host), ICounterActor
    {
        private int _count;

        public Task IncrementAsync(CancellationToken ct = default)
        {
            _count++;
            return Task.CompletedTask;
        }

        public Task<int> GetCountAsync(CancellationToken ct = default) =>
            Task.FromResult(_count);
    }

    public interface IGreeterActor : IVirtualActor
    {
        Task<string> GreetAsync(string name, CancellationToken ct = default);
    }

    public class GreeterActor(VirtualActorHost host) : VirtualActor(host), IGreeterActor
    {
        public Task<string> GreetAsync(string name, CancellationToken ct = default) =>
            Task.FromResult($"Hello, {name}!");
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddDaprVirtualActors_WithNoOptions_ServiceContainerBuildsSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors();

        using var provider = services.BuildServiceProvider();
        provider.ShouldNotBeNull();
    }

    [Fact]
    public void AddDaprVirtualActors_WithActorRegistration_RegistersInRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<CounterActor>(host => new CounterActor(host));
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorRegistrationRegistry>();

        registry.ShouldNotBeNull();
        registry.RegisteredActorTypes.ShouldContain("CounterActor");
    }

    [Fact]
    public void AddDaprVirtualActors_MultipleActors_AllRegisteredInRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<CounterActor>(host => new CounterActor(host));
            options.RegisterActor<GreeterActor>(host => new GreeterActor(host));
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorRegistrationRegistry>();

        registry.RegisteredActorTypes.Count.ShouldBe(2);
        registry.RegisteredActorTypes.ShouldContain("CounterActor");
        registry.RegisteredActorTypes.ShouldContain("GreeterActor");
    }

    [Fact]
    public void AddDaprVirtualActors_DuplicateActorType_ThrowsOnRegistryAccess()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<CounterActor>(host => new CounterActor(host));
            options.RegisterActor<CounterActor>(host => new CounterActor(host));
        });

        using var provider = services.BuildServiceProvider();

        // The registry singleton builds lazily; duplicate throws on first access
        Should.Throw<InvalidOperationException>(() =>
            provider.GetRequiredService<ActorRegistrationRegistry>());
    }

    [Fact]
    public void AddDaprVirtualActors_CustomActorTypeName_UsesProvidedName()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<CounterActor>(host => new CounterActor(host), "MyCounter");
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorRegistrationRegistry>();

        registry.RegisteredActorTypes.ShouldContain("MyCounter");
        registry.RegisteredActorTypes.ShouldNotContain("CounterActor");
    }

    [Fact]
    public void AddDaprVirtualActors_Options_IdleTimeoutConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.ActorIdleTimeout = TimeSpan.FromMinutes(5);
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<VirtualActorOptions>>();

        options.Value.ActorIdleTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void AddDaprVirtualActors_Options_ReentrancyConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.Reentrancy.Enabled = true;
            options.Reentrancy.MaxStackDepth = 32;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<VirtualActorOptions>>();

        options.Value.Reentrancy.Enabled.ShouldBeTrue();
        options.Value.Reentrancy.MaxStackDepth.ShouldBe(32);
    }

    [Fact]
    public void AddDaprVirtualActors_CalledTwice_DoesNotDuplicateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<CounterActor>(host => new CounterActor(host));
        });

        // Calling twice should not throw or duplicate registrations
        Should.NotThrow(() => services.AddDaprVirtualActors());

        using var provider = services.BuildServiceProvider();
        provider.ShouldNotBeNull();
    }

    [Fact]
    public void AddDaprVirtualActors_VirtualActorBuilder_IsReturned()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddDaprVirtualActors();

        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<VirtualActorBuilder>();
    }

    [Fact]
    public void AddDaprVirtualActors_NullServicesArgument_Throws()
    {
        IServiceCollection? services = null;
        Should.Throw<ArgumentNullException>(() => services!.AddDaprVirtualActors());
    }

    [Fact]
    public void RegisterActor_TypeInformationPopulatedCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<CounterActor>(host => new CounterActor(host));
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorRegistrationRegistry>();

        var reg = registry.GetRegistration("CounterActor");
        reg.ShouldNotBeNull();
        reg.TypeInformation.ActorTypeName.ShouldBe("CounterActor");
        reg.TypeInformation.ImplementationType.ShouldBe(typeof(CounterActor));
        reg.TypeInformation.InterfaceTypes.ShouldContain(typeof(ICounterActor));
    }

    [Fact]
    public void GetRegistration_UnknownType_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprVirtualActors();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorRegistrationRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.GetRegistration("NonExistentActor"));
    }

    [Fact]
    public void AddDaprVirtualActors_WithHostBuilder_BuildsSuccessfully()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDaprVirtualActors(options =>
                {
                    options.RegisterActor<CounterActor>(host => new CounterActor(host));
                });
            })
            .Build();

        host.ShouldNotBeNull();
        host.Services.GetRequiredService<ActorRegistrationRegistry>()
            .RegisteredActorTypes.ShouldContain("CounterActor");
    }
}
