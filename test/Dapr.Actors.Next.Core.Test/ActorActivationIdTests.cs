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

using System.Collections.Concurrent;
using System.Text;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Transport;
using Dapr.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class ActorActivationIdTests
{
    private const string ActorType = "Probe";
    private const string HeaderName = "Dapr-Actor-Activation-Id";
    private const string ReentrancyHeaderName = "Dapr-Reentrancy-Id";

    [Fact]
    public async Task Same_activation_id_reuses_activation_and_cached_state()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        await fixture.OverwriteFromAnotherHostAsync("one", 10);
        Assert.Equal(2, await fixture.IncrementAsync("one", 1, "a1"));

        var actor = Assert.Single(fixture.Created);
        Assert.Equal(["activate", "Increment", "Increment"], actor.Events);
        Assert.Equal(1, fixture.Store.Reads);
    }

    [Fact]
    public async Task New_activation_id_deactivates_old_and_rereads_state()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        await fixture.OverwriteFromAnotherHostAsync("one", 10);
        Assert.Equal(11, await fixture.IncrementAsync("one", 1, "a2"));

        Assert.Equal(2, fixture.Created.Count);
        var (old, fresh) = (fixture.Created[0], fixture.Created[1]);
        Assert.NotSame(old, fresh);
        Assert.Equal(["activate", "Increment", "deactivate"], old.Events);
        Assert.True(old.Disposed);
        Assert.Equal(["activate", "Increment"], fresh.Events);
        Assert.False(fresh.Disposed);
        Assert.Equal(2, fixture.Store.Reads);

        Assert.Equal(12, await fixture.IncrementAsync("one", 1, "a2"));
        Assert.Equal(2, fixture.Created.Count);
    }

    [Fact]
    public async Task Replaced_activation_cannot_save_from_deactivate()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        fixture.Created[0].SaveOnDeactivate = true;
        await fixture.OverwriteFromAnotherHostAsync("one", 10);
        Assert.Equal(11, await fixture.IncrementAsync("one", 1, "a2"));
        Assert.Contains("save refused", fixture.Created[0].Events);

        fixture.Created[1].SaveOnDeactivate = true;
        await fixture.DispatchAsync("one", ActorTurnKind.Deactivate, string.Empty, string.Empty, ActorHeaders.Empty);
        Assert.DoesNotContain("save refused", fixture.Created[1].Events);
        Assert.Equal(111, await fixture.IncrementAsync("one", 0, "a3"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Missing_activation_id_never_resets(string? missing)
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        Assert.Equal(2, await fixture.IncrementAsync("one", 1, missing));
        Assert.Equal(3, await fixture.IncrementAsync("one", 1, "a1"));

        var actor = Assert.Single(fixture.Created);
        Assert.DoesNotContain("deactivate", actor.Events);
        Assert.Equal(1, fixture.Store.Reads);
    }

    [Fact]
    public async Task No_activation_id_ever_keeps_existing_behaviour()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, null));
        Assert.Equal(2, await fixture.IncrementAsync("one", 1, null));

        Assert.Single(fixture.Created);
        Assert.Equal(1, fixture.Store.Reads);
    }

    [Fact]
    public async Task Stored_empty_activation_id_then_non_empty_resets()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, null));
        Assert.Equal(2, await fixture.IncrementAsync("one", 1, "a1"));

        Assert.Equal(2, fixture.Created.Count);
        Assert.Contains("deactivate", fixture.Created[0].Events);
        Assert.Equal(2, fixture.Store.Reads);
    }

    [Fact]
    public async Task Activation_id_header_name_is_case_insensitive()
    {
        await using var fixture = new Fixture();

        await fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Increment", "1", new Dictionary<string, string> { ["dapr-actor-activation-id"] = "a1" });
        await fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Increment", "1", new Dictionary<string, string> { ["DAPR-ACTOR-ACTIVATION-ID"] = "a1" });
        Assert.Single(fixture.Created);

        await fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Increment", "1", new Dictionary<string, string> { ["dapr-ACTOR-activation-ID"] = "a2" });
        Assert.Equal(2, fixture.Created.Count);
    }

    [Fact]
    public void Empty_exact_case_header_does_not_hide_other_case()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeaderName] = string.Empty,
            ["dapr-actor-activation-id"] = "a1",
        };

        Assert.Equal("a1", ActorHeaders.GetActivationId(headers));
    }

    [Fact]
    public void Empty_other_case_header_is_skipped()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dapr-actor-activation-id"] = string.Empty,
            ["DAPR-ACTOR-ACTIVATION-ID"] = "a1",
        };

        Assert.Equal("a1", ActorHeaders.GetActivationId(headers));
    }

    [Fact]
    public async Task Empty_duplicate_header_in_other_case_still_resets()
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "a1");
        await fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Increment", "1", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeaderName] = string.Empty,
            ["dapr-actor-activation-id"] = "a2",
        });

        Assert.Equal(2, fixture.Created.Count);
    }

    [Fact]
    public async Task Activation_id_value_is_compared_ordinally()
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "abc");
        await fixture.IncrementAsync("one", 1, "ABC");

        Assert.Equal(2, fixture.Created.Count);
    }

    [Fact]
    public async Task Activation_ids_are_tracked_per_actor()
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "a1");
        await fixture.IncrementAsync("two", 1, "b1");
        await fixture.IncrementAsync("one", 1, "a1");
        await fixture.IncrementAsync("two", 1, "b2");

        Assert.Equal(3, fixture.Created.Count);
        Assert.DoesNotContain("deactivate", fixture.Created[0].Events);
        Assert.Contains("deactivate", fixture.Created[1].Events);
    }

    [Theory]
    [InlineData(ActorTurnKind.Reminder, "Wake")]
    [InlineData(ActorTurnKind.Timer, "Tick")]
    public async Task Reminder_and_timer_with_same_activation_id_reuse(ActorTurnKind kind, string name)
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "a1");
        await fixture.DispatchAsync("one", kind, name, string.Empty, Headers("a1"));

        var actor = Assert.Single(fixture.Created);
        Assert.Equal(["activate", "Increment", name], actor.Events);
    }

    [Theory]
    [InlineData(ActorTurnKind.Reminder, "Wake")]
    [InlineData(ActorTurnKind.Timer, "Tick")]
    public async Task Reminder_and_timer_with_new_activation_id_reset(ActorTurnKind kind, string name)
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        await fixture.OverwriteFromAnotherHostAsync("one", 10);
        await fixture.DispatchAsync("one", kind, name, string.Empty, Headers("a2"));

        Assert.Equal(2, fixture.Created.Count);
        Assert.Equal(["activate", "Increment", "deactivate"], fixture.Created[0].Events);
        Assert.Equal(["activate", name], fixture.Created[1].Events);
        Assert.Equal(11, await fixture.IncrementAsync("one", 1, "a2"));
    }

    [Theory]
    [InlineData(ActorTurnKind.Reminder, "Wake")]
    [InlineData(ActorTurnKind.Timer, "Tick")]
    public async Task Reminder_and_timer_without_activation_id_never_reset(ActorTurnKind kind, string name)
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "a1");
        await fixture.DispatchAsync("one", kind, name, string.Empty, ActorHeaders.Empty);

        Assert.Single(fixture.Created);
    }

    [Fact]
    public async Task Deactivate_then_same_activation_id_activates_fresh()
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "a1");
        await fixture.DispatchAsync("one", ActorTurnKind.Deactivate, string.Empty, string.Empty, ActorHeaders.Empty);
        await fixture.IncrementAsync("one", 1, "a1");

        Assert.Equal(2, fixture.Created.Count);
        Assert.Contains("deactivate", fixture.Created[0].Events);
        Assert.DoesNotContain("deactivate", fixture.Created[1].Events);
    }

    [Fact]
    public async Task Reset_with_throwing_deactivate_still_runs_new_call_and_disposes_old()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        fixture.Created[0].ThrowOnDeactivate = true;
        await fixture.OverwriteFromAnotherHostAsync("one", 10);

        Assert.Equal(11, await fixture.IncrementAsync("one", 1, "a2"));

        Assert.Equal(2, fixture.Created.Count);
        var (old, fresh) = (fixture.Created[0], fixture.Created[1]);
        Assert.Equal(["activate", "Increment", "deactivate"], old.Events);
        Assert.True(old.Disposed);
        Assert.Equal(["activate", "Increment"], fresh.Events);
        Assert.Equal(12, await fixture.IncrementAsync("one", 1, "a2"));
    }

    [Fact]
    public async Task Deactivate_with_throwing_deactivate_still_throws_and_disposes()
    {
        await using var fixture = new Fixture();

        await fixture.IncrementAsync("one", 1, "a1");
        fixture.Created[0].ThrowOnDeactivate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DispatchAsync("one", ActorTurnKind.Deactivate, string.Empty, string.Empty, ActorHeaders.Empty));

        Assert.True(fixture.Created[0].Disposed);
        await fixture.IncrementAsync("one", 1, "a1");
        Assert.Equal(2, fixture.Created.Count);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Stream_callbacks_carry_activation_id_to_runtime()
    {
        var harness = new InMemoryTransportHarness();
        await using var fixture = new Fixture(harness);
        var service = fixture.Provider.GetRequiredService<SubscribeActorEventsStreamManager>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(cts.Token);
        var stream = await harness.WaitForStreamAsync(cts.Token);
        await stream.ReceiveAsync(cts.Token);

        async Task<SubscribeActorEventsResponse> SendAsync(string id, SubscribeActorEventsFrameKind kind, string method, string payload, IReadOnlyDictionary<string, string> headers)
        {
            await stream.SendAsync(new SubscribeActorEventsRequest(id, kind, ActorType, "one", method, Encoding.UTF8.GetBytes(payload), headers), cts.Token);
            return await stream.ReceiveAsync(cts.Token);
        }

        var first = await SendAsync("1", SubscribeActorEventsFrameKind.Invoke, "Increment", "1", Headers("a1"));
        var same = await SendAsync("2", SubscribeActorEventsFrameKind.Invoke, "Increment", "1", new Dictionary<string, string> { ["dapr-actor-activation-id"] = "a1" });
        await SendAsync("3", SubscribeActorEventsFrameKind.Reminder, "Wake", string.Empty, Headers("a1"));
        await SendAsync("4", SubscribeActorEventsFrameKind.Timer, "Tick", string.Empty, Headers("a2"));
        var afterReset = await SendAsync("5", SubscribeActorEventsFrameKind.Invoke, "Increment", "1", Headers("a2"));
        await service.StopAsync(cts.Token);

        Assert.Equal("1", Encoding.UTF8.GetString(first.Payload.Span));
        Assert.Equal("2", Encoding.UTF8.GetString(same.Payload.Span));
        Assert.Equal("3", Encoding.UTF8.GetString(afterReset.Payload.Span));
        Assert.Equal(2, fixture.Created.Count);
        Assert.Equal(["activate", "Increment", "Increment", "Wake", "deactivate"], fixture.Created[0].Events);
        Assert.Equal(["activate", "Tick", "Increment"], fixture.Created[1].Events);
    }

    [Theory]
    [InlineData("1.18.9")]
    [InlineData("1.19.0-rc.7")]
    [InlineData("edge")]
    public async Task Stream_callbacks_from_older_sidecar_ignore_activation_id(string runtimeVersion)
    {
        var harness = new InMemoryTransportHarness();
        await using var fixture = new Fixture(harness, runtimeVersion);
        var service = fixture.Provider.GetRequiredService<SubscribeActorEventsStreamManager>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(cts.Token);
        var stream = await harness.WaitForStreamAsync(cts.Token);
        await stream.ReceiveAsync(cts.Token);

        async Task<SubscribeActorEventsResponse> SendAsync(string id, SubscribeActorEventsFrameKind kind, string method, string payload, IReadOnlyDictionary<string, string> headers)
        {
            await stream.SendAsync(new SubscribeActorEventsRequest(id, kind, ActorType, "one", method, Encoding.UTF8.GetBytes(payload), headers), cts.Token);
            return await stream.ReceiveAsync(cts.Token);
        }

        await SendAsync("1", SubscribeActorEventsFrameKind.Invoke, "Increment", "1", Headers("a1"));
        await SendAsync("2", SubscribeActorEventsFrameKind.Invoke, "Increment", "1", new Dictionary<string, string> { [HeaderName] = string.Empty, ["dapr-actor-activation-id"] = "a2" });
        await SendAsync("3", SubscribeActorEventsFrameKind.Reminder, "Wake", string.Empty, Headers("a3"));
        var last = await SendAsync("4", SubscribeActorEventsFrameKind.Invoke, "Increment", "1", Headers("a4"));
        await service.StopAsync(cts.Token);

        Assert.Equal("3", Encoding.UTF8.GetString(last.Payload.Span));
        Assert.Single(fixture.Created);
        Assert.Equal(["activate", "Increment", "Increment", "Wake", "Increment"], fixture.Created[0].Events);
    }

    [Fact]
    public async Task New_activation_id_admitted_mid_turn_stops_that_turn_saving()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        var first = fixture.Created[0];
        var held = fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Hold", "", Headers("a1"));
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var replacing = fixture.IncrementAsync("one", 1, "a2");
        Assert.False(replacing.IsCompleted);
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Equal(2, await replacing);
        Assert.Equal(2, fixture.Created.Count);
        Assert.Equal(["activate", "Increment", "Hold", "deactivate"], first.Events);
    }

    [Fact]
    public async Task Reentrant_call_with_other_activation_id_keeps_parent_activation()
    {
        await using var fixture = new Fixture();

        Assert.Equal(1, await fixture.IncrementAsync("one", 1, "a1"));
        var first = fixture.Created[0];
        var held = fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Hold", "", new Dictionary<string, string> { [HeaderName] = "a1", [ReentrancyHeaderName] = "chain" });
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        try
        {
            var nested = await fixture.DispatchAsync("one", ActorTurnKind.Invoke, "Increment", "1", new Dictionary<string, string> { [HeaderName] = "a2", [ReentrancyHeaderName] = "chain" })
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal("1002", Encoding.UTF8.GetString(nested!));
        }
        finally
        {
            first.Release.TrySetResult();
        }

        await held;

        Assert.Same(first, Assert.Single(fixture.Created));
        Assert.Equal(["activate", "Increment", "Hold", "Increment"], first.Events);
        Assert.Equal(1003, await fixture.IncrementAsync("one", 1, "a1"));
    }

    [Fact]
    public void Without_activation_id_removes_every_casing()
    {
        var headers = ActorHeaders.WithoutActivationId(new Dictionary<string, string>
        {
            [HeaderName] = "a1",
            ["dapr-actor-activation-id"] = "a2",
            ["Dapr-Reentrancy-Id"] = "chain",
        });

        Assert.Equal(["Dapr-Reentrancy-Id"], headers.Keys);
    }

    private static Dictionary<string, string> Headers(string activationId) => new() { [HeaderName] = activationId };

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(InMemoryTransportHarness? harness = null, string runtimeVersion = "1.19.1")
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInMemoryActorAdapters();
            services.AddSingleton<IActorStateStore>(Store);
            if (harness is not null)
            {
                services.AddSingleton<ISubscribeActorEventsTransport>(harness);
                services.AddSingleton(new ActorActivationIdGate(_ => Task.FromResult<string?>(runtimeVersion)));
            }

            services.AddDaprActorsCore(registrations =>
            {
                registrations.Add(
                    ActorType,
                    typeof(IProbeActor),
                    typeof(ProbeActor),
                    (sp, _) =>
                    {
                        var actor = new ProbeActor(sp.GetRequiredService<ActorActivationContext>());
                        Created.Add(actor);
                        return actor;
                    },
                    new ProbeDispatcher(),
                    new ActorLifecycle(
                        static (actor, _) => ((ProbeActor)actor).Record("activate"),
                        static (actor, _) => ((ProbeActor)actor).DeactivateAsync(),
                        static (_, _, _) => ValueTask.CompletedTask,
                        static (_, _, _, _) => ValueTask.CompletedTask));
            });

            Provider = services.BuildServiceProvider(validateScopes: true);
            Runtime = Provider.GetRequiredService<IActorRuntime>();
        }

        public CountingStateStore Store { get; } = new();

        public List<ProbeActor> Created { get; } = [];

        public ServiceProvider Provider { get; }

        public IActorRuntime Runtime { get; }

        public async Task<int> IncrementAsync(string actorId, int amount, string? activationId)
        {
            var headers = activationId is null ? ActorHeaders.Empty : Headers(activationId);
            var result = await DispatchAsync(actorId, ActorTurnKind.Invoke, "Increment", amount.ToString(), headers);
            return int.Parse(Encoding.UTF8.GetString(result!));
        }

        public Task<byte[]?> DispatchAsync(string actorId, ActorTurnKind kind, string operation, string payload, IReadOnlyDictionary<string, string> headers) =>
            Runtime.DispatchAsync(
                new ActorRuntimeRequest(ActorType, ActorId.Create(actorId), operation, kind, Encoding.UTF8.GetBytes(payload), headers, new ActorRequestContext(null, null, new Dictionary<string, string>())),
                TestContext.Current.CancellationToken);

        public async Task OverwriteFromAnotherHostAsync(string actorId, int value)
        {
            var donor = $"donor-{Guid.NewGuid():N}";
            var reads = Store.Reads;
            await IncrementAsync(donor, value, null);
            var bytes = await Store.ReadAsync(ActorType, donor, "state", TestContext.Current.CancellationToken);
            await Store.WriteAsync(ActorType, actorId, "state", bytes!.Value, TestContext.Current.CancellationToken);
            Created.RemoveAll(actor => actor.ActorId.Value == donor);
            Store.Reads = reads;
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private interface IProbeActor : IActor
    {
    }

    private sealed class ProbeActor(ActorActivationContext context) : Actor, IDisposable
    {
        public List<string> Events { get; } = [];

        public bool Disposed { get; private set; }

        public bool ThrowOnDeactivate { get; set; }

        public ActorId ActorId => context.ActorId;

        protected override ActorId Id => context.ActorId;

        protected override IActorStateAccessor State => context.State;

        public ValueTask Record(string name)
        {
            Events.Add(name);
            return ValueTask.CompletedTask;
        }

        public bool SaveOnDeactivate { get; set; }

        public async ValueTask DeactivateAsync()
        {
            Events.Add("deactivate");
            if (ThrowOnDeactivate)
            {
                throw new InvalidOperationException("deactivate failed");
            }

            if (SaveOnDeactivate)
            {
                var state = await State.GetOrCreateAsync("state", () => new CounterState());
                state.Value.Value += 100;
                try
                {
                    await State.SaveStateAsync();
                }
                catch (InvalidOperationException)
                {
                    Events.Add("save refused");
                    throw;
                }
            }
        }

        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task HoldAsync(CancellationToken cancellationToken)
        {
            Events.Add("Hold");
            var state = await State.GetOrCreateAsync("state", () => new CounterState(), cancellationToken);
            state.Value.Value += 1000;
            Held.SetResult();
            await Release.Task;
        }

        public async Task<int> IncrementAsync(int amount, CancellationToken cancellationToken)
        {
            Events.Add("Increment");
            var state = await State.GetOrCreateAsync("state", () => new CounterState(), cancellationToken);
            state.Value.Value += amount;
            return state.Value.Value;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class ProbeDispatcher : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var probe = (ProbeActor)actor;
            if (request.MethodName == "Increment")
            {
                var value = await probe.IncrementAsync(int.Parse(Encoding.UTF8.GetString(request.Payload.Span)), cancellationToken);
                return new ActorDispatchResponse(Encoding.UTF8.GetBytes(value.ToString()));
            }

            if (request.MethodName == "Hold")
            {
                await probe.HoldAsync(cancellationToken);
                return new ActorDispatchResponse(Array.Empty<byte>());
            }

            await probe.Record(request.MethodName);
            return new ActorDispatchResponse(Array.Empty<byte>());
        }
    }

    private sealed class CountingStateStore : IActorStateStore
    {
        private readonly ConcurrentDictionary<string, ReadOnlyMemory<byte>> values = new(StringComparer.Ordinal);

        public int Reads { get; set; }

        public ValueTask ApplyAsync(string actorType, string actorId, IReadOnlyList<ActorStateOperation> operations, CancellationToken cancellationToken = default)
        {
            foreach (var operation in operations)
            {
                if (operation.Value is { } value)
                {
                    values[Key(actorType, actorId, operation.Name)] = value.ToArray();
                }
                else
                {
                    values.TryRemove(Key(actorType, actorId, operation.Name), out _);
                }
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
        {
            Reads++;
            return values.TryGetValue(Key(actorType, actorId, name), out var value)
                ? ValueTask.FromResult<ReadOnlyMemory<byte>?>(value)
                : ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
        }

        public ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            values[Key(actorType, actorId, name)] = value.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
        {
            values.TryRemove(Key(actorType, actorId, name), out _);
            return ValueTask.CompletedTask;
        }

        private static string Key(string actorType, string actorId, string name) => $"{actorType}||{actorId}||{name}";
    }
}
