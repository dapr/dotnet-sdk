// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dapr.Actors.Runtime
{
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
            return new DependencyInjectionActorActivator(CreateServices(), ActorTypeInformation.Get(type));
        }

        [Fact]
        public async Task CreateAsync_CanActivateWithDI()
        {
            var activator = CreateActivator(typeof(TestActor));

            var host = new ActorHost(ActorTypeInformation.Get(typeof(TestActor)), ActorId.CreateRandom(), JsonSerializerDefaults.Web, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory);
            var state = await activator.CreateAsync(host);
            var actor = Assert.IsType<TestActor>(state.Actor);

            Assert.NotNull(actor.SingletonService);
            Assert.NotNull(actor.ScopedService);
        }

        [Fact]
        public async Task CreateAsync_CreatesNewScope()
        {
            var activator = CreateActivator(typeof(TestActor));

            var host1 = new ActorHost(ActorTypeInformation.Get(typeof(TestActor)), ActorId.CreateRandom(), JsonSerializerDefaults.Web, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory);
            var state1 = await activator.CreateAsync(host1);
            var actor1 = Assert.IsType<TestActor>(state1.Actor);

            var host2 = new ActorHost(ActorTypeInformation.Get(typeof(TestActor)), ActorId.CreateRandom(), JsonSerializerDefaults.Web, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory);
            var state2 = await activator.CreateAsync(host2);
            var actor2 = Assert.IsType<TestActor>(state2.Actor);

            Assert.Same(actor1.SingletonService, actor2.SingletonService);
            Assert.NotSame(actor1.ScopedService, actor2.ScopedService);
        }

        [Fact]
        public async Task DeleteAsync_DisposesScope()
        {
            var activator = CreateActivator(typeof(TestActor));

            var host = new ActorHost(ActorTypeInformation.Get(typeof(TestActor)), ActorId.CreateRandom(), JsonSerializerDefaults.Web, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory);
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

            var host = new ActorHost(ActorTypeInformation.Get(typeof(DisposableActor)), ActorId.CreateRandom(), JsonSerializerDefaults.Web, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory);
            var state = await activator.CreateAsync(host);
            var actor = Assert.IsType<DisposableActor>(state.Actor);

            await activator.DeleteAsync(state); // does not throw

            Assert.True(actor.IsDisposed);
        }

        [Fact]
        public async Task DeleteAsync_AsyncDisposable()
        {
            var activator = CreateActivator(typeof(AsyncDisposableActor));

            var host = new ActorHost(ActorTypeInformation.Get(typeof(AsyncDisposableActor)), ActorId.CreateRandom(), JsonSerializerDefaults.Web, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory);
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
}
