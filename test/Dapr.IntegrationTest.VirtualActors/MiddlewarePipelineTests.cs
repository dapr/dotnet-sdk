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
using Shouldly;

namespace Dapr.IntegrationTest.VirtualActors;

/// <summary>
/// Integration tests for <see cref="IActorMiddleware"/>, <see cref="IActorLifecycleObserver"/>,
/// and the <see cref="VirtualActorBuilder"/> extensibility pipeline.
/// </summary>
public class MiddlewarePipelineTests
{
    // ---------------------------------------------------------------------------
    // Tracking middleware for testing
    // ---------------------------------------------------------------------------

    private sealed class TrackingMiddleware : IActorMiddleware
    {
        public List<string> InvokedBefore { get; } = [];
        public List<string> InvokedAfter { get; } = [];

        public async Task InvokeAsync(ActorInvocationContext context, ActorMiddlewareDelegate next,
            CancellationToken cancellationToken = default)
        {
            InvokedBefore.Add(context.MethodName);
            await next(context, cancellationToken);
            InvokedAfter.Add(context.MethodName);
        }
    }

    private sealed class TrackingLifecycleObserver : IActorLifecycleObserver
    {
        public List<string> Activated { get; } = [];
        public List<string> Deactivated { get; } = [];

        public Task OnActivatedAsync(string actorType, VirtualActorId actorId, CancellationToken cancellationToken = default)
        {
            Activated.Add($"{actorType}/{actorId.GetId()}");
            return Task.CompletedTask;
        }

        public Task OnDeactivatedAsync(string actorType, VirtualActorId actorId, CancellationToken cancellationToken = default)
        {
            Deactivated.Add($"{actorType}/{actorId.GetId()}");
            return Task.CompletedTask;
        }

        public Task OnMethodCompletedAsync(ActorInvocationContext context, TimeSpan elapsed, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OnMethodFailedAsync(ActorInvocationContext context, Exception exception, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void VirtualActorBuilder_UseMiddleware_RegistersMiddlewareInDI()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var middleware = new TrackingMiddleware();
        services.AddDaprVirtualActors()
            .UseMiddleware(middleware);

        using var provider = services.BuildServiceProvider();

        // Middleware should be resolvable as IActorMiddleware
        var resolved = provider.GetServices<IActorMiddleware>().ToList();
        resolved.ShouldContain(middleware);
    }

    [Fact]
    public void VirtualActorBuilder_AddLifecycleObserver_RegistersObserverInDI()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var observer = new TrackingLifecycleObserver();
        services.AddDaprVirtualActors()
            .AddLifecycleObserver(observer);

        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetServices<IActorLifecycleObserver>().ToList();
        resolved.ShouldContain(observer);
    }

    [Fact]
    public void VirtualActorBuilder_MultipleMiddleware_AllRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDaprVirtualActors()
            .UseMiddleware<TrackingMiddleware>()
            .UseMiddleware<TrackingMiddleware>();

        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetServices<IActorMiddleware>().ToList();
        resolved.Count.ShouldBe(2);
    }

    [Fact]
    public void VirtualActorBuilder_MultipleLifecycleObservers_AllRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDaprVirtualActors()
            .AddLifecycleObserver<TrackingLifecycleObserver>()
            .AddLifecycleObserver<TrackingLifecycleObserver>();

        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetServices<IActorLifecycleObserver>().ToList();
        resolved.Count.ShouldBe(2);
    }

    [Fact]
    public void VirtualActorBuilder_UseActivator_RegistersCustomActivator()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDaprVirtualActors()
            .UseActorActivator<DependencyInjectionActorActivator>();

        using var provider = services.BuildServiceProvider();

        var activator = provider.GetRequiredService<IActorActivator>();
        activator.ShouldBeOfType<DependencyInjectionActorActivator>();
    }

    [Fact]
    public void VirtualActorBuilder_Services_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddDaprVirtualActors();

        builder.Services.ShouldBeSameAs(services);
    }

    [Fact]
    public async Task TrackingLifecycleObserver_WhenCalledDirectly_TracksEvents()
    {
        var observer = new TrackingLifecycleObserver();
        var actorId = new VirtualActorId("actor-1");

        await observer.OnActivatedAsync("TestActor", actorId,
            TestContext.Current.CancellationToken);
        await observer.OnDeactivatedAsync("TestActor", actorId,
            TestContext.Current.CancellationToken);

        observer.Activated.ShouldContain("TestActor/actor-1");
        observer.Deactivated.ShouldContain("TestActor/actor-1");
    }
}
