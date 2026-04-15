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

using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Dapr.VirtualActors.Runtime.Test;

public class VirtualActorServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprVirtualActors_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<GreeterActor>(host => new GreeterActor(host));
        });

        // Verify key services are registered
        services.ShouldContain(s => s.ServiceType == typeof(IActorActivator));
        services.ShouldContain(s => s.ServiceType == typeof(IVirtualActorProxyFactory));
        services.ShouldContain(s => s.ServiceType == typeof(IActorTimerManager));
        services.ShouldContain(s => s.ServiceType == typeof(ActorRegistrationRegistry));
    }

    [Fact]
    public void AddDaprVirtualActors_ReturnsBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<GreeterActor>(host => new GreeterActor(host));
        });

        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<VirtualActorBuilder>();
    }

    [Fact]
    public void AddDaprVirtualActors_WithoutConfigure_Works()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprVirtualActors();
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void Builder_UseMiddleware_RegistersMiddleware()
    {
        var services = new ServiceCollection();
        services.AddDaprVirtualActors()
            .UseMiddleware<TestMiddleware>();

        services.ShouldContain(s =>
            s.ServiceType == typeof(IActorMiddleware) &&
            s.ImplementationType == typeof(TestMiddleware));
    }

    [Fact]
    public void Builder_AddLifecycleObserver_RegistersObserver()
    {
        var services = new ServiceCollection();
        services.AddDaprVirtualActors()
            .AddLifecycleObserver<TestLifecycleObserver>();

        services.ShouldContain(s =>
            s.ServiceType == typeof(IActorLifecycleObserver) &&
            s.ImplementationType == typeof(TestLifecycleObserver));
    }

    private class TestMiddleware : IActorMiddleware
    {
        public Task InvokeAsync(ActorInvocationContext context, ActorMiddlewareDelegate next, CancellationToken ct = default)
            => next(context, ct);
    }

    private class TestLifecycleObserver : IActorLifecycleObserver
    {
        public Task OnActivatedAsync(string actorType, VirtualActorId actorId, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnDeactivatedAsync(string actorType, VirtualActorId actorId, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnMethodCompletedAsync(ActorInvocationContext context, TimeSpan elapsed, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnMethodFailedAsync(ActorInvocationContext context, Exception exception, CancellationToken ct = default) => Task.CompletedTask;
    }
}
