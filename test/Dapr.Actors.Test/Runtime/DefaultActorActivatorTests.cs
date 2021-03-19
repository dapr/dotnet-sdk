// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dapr.Actors.Runtime
{
    public class DefaultActorActivatorTests
    {
        [Fact]
        public async Task CreateAsync_CallsConstructor()
        {
            var activator = new DefaultActorActivator();

            var host = ActorHost.CreateForTest<TestActor>();
            var state = await activator.CreateAsync(host);
            Assert.IsType<TestActor>(state.Actor);
        }

        [Fact]
        public async Task DeleteAsync_NotDisposable()
        {
            var activator = new DefaultActorActivator();

            var host = ActorHost.CreateForTest<TestActor>();
            var actor = new TestActor(host);
            var state = new ActorActivatorState(actor);

            await activator.DeleteAsync(state); // does not throw
        }

        [Fact]
        public async Task DeleteAsync_Disposable()
        {
            var activator = new DefaultActorActivator();

            var host = ActorHost.CreateForTest<DisposableActor>();
            var actor = new DisposableActor(host);
            var state = new ActorActivatorState(actor);

            await activator.DeleteAsync(state); // does not throw

            Assert.True(actor.IsDisposed);
        }

        [Fact]
        public async Task DeleteAsync_AsyncDisposable()
        {
            var activator = new DefaultActorActivator();

            var host = ActorHost.CreateForTest<AsyncDisposableActor>();
            var actor = new AsyncDisposableActor(host);
            var state = new ActorActivatorState(actor);

            await activator.DeleteAsync(state);

            Assert.True(actor.IsDisposed);
        }

        private interface ITestActor : IActor
        {
        }

        private class TestActor : Actor, ITestActor
        {
            public TestActor(ActorHost host)
                : base(host)
            {
            }
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
