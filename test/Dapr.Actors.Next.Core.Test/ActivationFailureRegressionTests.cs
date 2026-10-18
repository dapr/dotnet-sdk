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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class ActivationFailureRegressionTests
{
    [Fact]
    public async Task Failed_activation_is_disposed_and_retried_without_its_dirty_state()
    {
        await VerifyActivationRetry(cancel: false);
    }

    [Fact]
    public async Task Cancelled_activation_is_disposed_and_retried_without_its_dirty_state()
    {
        await VerifyActivationRetry(cancel: true);
    }

    private static async Task VerifyActivationRetry(bool cancel)
    {
        var actors = new List<ProbeActor>();
        var servicesDisposed = new List<ScopedResource>();
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        var deactivations = 0;
        var lifecycle = new ActorLifecycle(
            async (actor, token) =>
            {
                if (++attempts == 1)
                {
                    await ((ProbeActor)actor).Context.State.SetAsync("value", 99, token);
                    await Task.Yield();
                    if (cancel)
                    {
                        cancellation.Cancel();
                        token.ThrowIfCancellationRequested();
                    }

                    throw new InvalidOperationException("activation failed");
                }
            },
            (_, _) => { deactivations++; return ValueTask.CompletedTask; },
            (_, _, _) => ValueTask.CompletedTask,
            (_, _, _, _) => ValueTask.CompletedTask);
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddScoped(_ =>
            {
                var resource = new ScopedResource();
                servicesDisposed.Add(resource);
                return resource;
            });
            services.AddDaprActorsCore(registrations => registrations.Add(
                "Probe", typeof(IActor), typeof(ProbeActor),
                (sp, _) =>
                {
                    sp.GetRequiredService<ScopedResource>();
                    var actor = new ProbeActor(sp.GetRequiredService<ActorActivationContext>());
                    actors.Add(actor);
                    return actor;
                },
                new ProbeDispatcher(), lifecycle));
        });
        var id = ActorId.Create("one");
        var failed = runtime.InvokeAsync("Probe", id, "Read", cancellationToken: cancellation.Token);
        await runtime.RunToIdle();
        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => failed);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => failed);
        }

        Assert.Equal(1, actors[0].Disposals);
        Assert.Equal(1, servicesDisposed[0].Disposals);
        Assert.Equal(0, deactivations);
        var retry = runtime.InvokeAsync("Probe", id, "Read");
        await runtime.RunToIdle();
        Assert.Equal("1", System.Text.Encoding.UTF8.GetString((await retry)!));
        Assert.Equal(2, attempts);
        Assert.Equal(2, actors.Count);
        var deactivate = runtime.DeactivateAsync("Probe", id);
        await runtime.RunToIdle();
        await deactivate;
        Assert.Equal(1, deactivations);
        Assert.All(actors, actor => Assert.Equal(1, actor.Disposals));
        Assert.All(servicesDisposed, resource => Assert.Equal(1, resource.Disposals));
    }

    [Fact]
    public async Task Throwing_factory_disposes_lazy_scope_and_next_turn_retries()
    {
        var resources = new List<ScopedResource>();
        var attempts = 0;
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddScoped(_ =>
            {
                var resource = new ScopedResource();
                resources.Add(resource);
                return resource;
            });
            services.AddDaprActorsCore(registrations => registrations.Add(
                "Probe", typeof(IActor), typeof(ProbeActor),
                (sp, _) =>
                {
                    sp.GetRequiredService<ScopedResource>();
                    if (++attempts == 1)
                    {
                        throw new InvalidOperationException("constructor failed");
                    }

                    return new ProbeActor(sp.GetRequiredService<ActorActivationContext>());
                }, new ProbeDispatcher()));
        });
        var id = ActorId.Create("one");
        var first = runtime.InvokeAsync("Probe", id, "Read");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        Assert.Equal(1, resources[0].Disposals);
        var retry = runtime.InvokeAsync("Probe", id, "Read");
        await runtime.RunToIdle();
        await retry;
        var deactivate = runtime.DeactivateAsync("Probe", id);
        await runtime.RunToIdle();
        await deactivate;
        Assert.Equal(2, attempts);
        Assert.All(resources, resource => Assert.Equal(1, resource.Disposals));
    }

    private sealed class ScopedResource : IAsyncDisposable
    {
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    private sealed class ProbeActor(ActorActivationContext context) : IActor, IAsyncDisposable
    {
        public ActorActivationContext Context { get; } = context;
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    private sealed class ProbeDispatcher : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(
            IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var state = await ((ProbeActor)actor).Context.State.GetOrCreateAsync("value", () => 1, cancellationToken);
            return new ActorDispatchResponse(System.Text.Encoding.UTF8.GetBytes(state.Value.ToString()));
        }
    }
}
