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

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Actors.Runtime;

public class DependencyInjectionActorActivatorTests
{
    private IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestScopedService>();
        services.AddSingleton<TestSingletonService>();
        return services.BuildServiceProvider(new ServiceProviderOptions() { ValidateScopes = true, });
    }

    private DependencyInjectionActorActivator CreateActivator(Type type)
    {
        return new DependencyInjectionActorActivator(CreateServices(), ActorTypeInformation.Get(type, actorTypeName: null));
    }

    [Fact]
    public async Task CreateAsync_CanActivateWithDI()
    {
        var activator = CreateActivator(typeof(TestActor));

        var host = ActorHost.CreateForTest<TestActor>();
        var state = await activator.CreateAsync(host);
        var actor = Assert.IsType<TestActor>(state.Actor);

        Assert.NotNull(actor.SingletonService);
        Assert.NotNull(actor.ScopedService);
    }

    [Fact]
    public async Task CreateAsync_CreatesNewScope()
    {
        var activator = CreateActivator(typeof(TestActor));

        var host1 = ActorHost.CreateForTest<TestActor>();
        var state1 = await activator.CreateAsync(host1);
        var actor1 = Assert.IsType<TestActor>(state1.Actor);

        var host2 = ActorHost.CreateForTest<TestActor>();
        var state2 = await activator.CreateAsync(host2);
        var actor2 = Assert.IsType<TestActor>(state2.Actor);

        Assert.Same(actor1.SingletonService, actor2.SingletonService);
        Assert.NotSame(actor1.ScopedService, actor2.ScopedService);
    }

    [Fact]
    public async Task CreateAsync_CustomJsonOptions()
    {
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = false };
        var activator = CreateActivator(typeof(TestActor));

        var host = ActorHost.CreateForTest<TestActor>(new ActorTestOptions { JsonSerializerOptions = jsonOptions });
        var state = await activator.CreateAsync(host);

        Assert.Same(jsonOptions, state.Actor.Host.JsonSerializerOptions);
    }

    [Fact]
    public async Task DeleteAsync_DisposesScope()
    {
        var activator = CreateActivator(typeof(TestActor));

        var host = ActorHost.CreateForTest<TestActor>();
        var state = await activator.CreateAsync(host);
        var actor = Assert.IsType<TestActor>(state.Actor);

        Assert.False(actor.ScopedService.IsDisposed);

        await activator.DeleteAsync(state);

        Assert.True(actor.ScopedService.IsDisposed);
    }

    [Fact]
    public async Task DeleteAsync_Disposable()
    {
        var activator = CreateActivator(typeof(DisposableActor));

        var host = ActorHost.CreateForTest<DisposableActor>();
        var state = await activator.CreateAsync(host);
        var actor = Assert.IsType<DisposableActor>(state.Actor);

        await activator.DeleteAsync(state); // does not throw

        Assert.True(actor.IsDisposed);
    }

    [Fact]
    public async Task DeleteAsync_AsyncDisposable()
    {
        var activator = CreateActivator(typeof(AsyncDisposableActor));

        var host = ActorHost.CreateForTest<AsyncDisposableActor>();
        var state = await activator.CreateAsync(host);
        var actor = Assert.IsType<AsyncDisposableActor>(state.Actor);

        await activator.DeleteAsync(state);

        Assert.True(actor.IsDisposed);
    }

    private class TestSingletonService
    {
    }

    private class TestScopedService : IDisposable
    {
        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private interface ITestActor : IActor
    {
    }

    private class TestActor : Actor, ITestActor
    {
        public TestActor(ActorHost host, TestSingletonService singletonService, TestScopedService scopedService)
            : base(host)
        {
            this.SingletonService = singletonService;
            this.ScopedService = scopedService;
        }

        public TestSingletonService SingletonService { get; }
        public TestScopedService ScopedService { get; }
    }

    private class DisposableActor : Actor, ITestActor, IDisposable
    {
        public DisposableActor(ActorHost host)
            : base(host)
        {
        }

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private class AsyncDisposableActor : Actor, ITestActor, IAsyncDisposable
    {
        public AsyncDisposableActor(ActorHost host)
            : base(host)
        {
        }

        public bool IsDisposed { get; set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return new ValueTask();
        }
    }
}