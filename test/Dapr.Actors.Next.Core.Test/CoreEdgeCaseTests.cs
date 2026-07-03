using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Scheduling;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Core.Transport;
using Dapr.Common.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class CoreEdgeCaseTests
{
    [Fact]
    public async Task State_accessor_supports_set_remove_missing_and_dirty_setter()
    {
        var store = new InMemoryActorStateStore();
        var serializer = new ActorWireSerializer(new JsonDaprSerializer());
        var state = new ActorStateUnitOfWork("Counter", ActorId.Create("s1"), store, serializer);

        Assert.Null(await state.TryGetAsync<CounterState>("missing"));
        await state.SetAsync("state", new CounterState { Value = 10 }, 3);
        await state.FlushAsync();
        var loaded = await state.TryGetAsync<CounterState>("state");
        loaded!.Value = new CounterState { Value = 11 };
        await state.FlushAsync();
        await state.RemoveAsync("state");
        Assert.Null(await state.TryGetAsync<CounterState>("state"));
        await state.FlushAsync();

        Assert.Null(await store.ReadAsync("Counter", "s1", "state"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await state.TryGetAsync<CounterState>(""));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await state.GetOrCreateAsync<CounterState>("x", null!));
    }

    [Fact]
    public async Task Loaded_state_stays_clean_until_it_changes()
    {
        var store = new RecordingActorStateStore();
        var serializer = new ActorWireSerializer(new JsonDaprSerializer());
        var firstTurn = new ActorStateUnitOfWork("Counter", ActorId.Create("clean"), store, serializer);

        await firstTurn.SetAsync("state", new CounterState { Value = 10 }, 1);
        await firstTurn.FlushAsync();

        var secondTurn = new ActorStateUnitOfWork("Counter", ActorId.Create("clean"), store, serializer);
        var loaded = await secondTurn.TryGetAsync<CounterState>("state");

        Assert.NotNull(loaded);
        Assert.Equal(10, loaded.Value.Value);
        Assert.Equal(1, store.WriteCount);

        await secondTurn.FlushAsync();

        Assert.Equal(1, store.WriteCount);

        loaded.Value.Value = 11;
        await secondTurn.FlushAsync();

        Assert.Equal(2, store.WriteCount);

        await secondTurn.FlushAsync();

        Assert.Equal(2, store.WriteCount);
    }

    [Fact]
    public async Task State_accessor_reports_null_envelope()
    {
        var store = new InMemoryActorStateStore();
        await store.WriteAsync("Counter", "null", "state", System.Text.Encoding.UTF8.GetBytes("null"));
        var state = new ActorStateUnitOfWork("Counter", ActorId.Create("null"), store, new NullEnvelopeWireSerializer());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await state.TryGetAsync<CounterState>("state"));
    }

    [Fact]
    public async Task State_accessor_reports_bad_envelope()
    {
        var store = new InMemoryActorStateStore();
        var serializer = new ActorWireSerializer(new JsonDaprSerializer());
        await store.WriteAsync("Counter", "bad", "state", System.Text.Encoding.UTF8.GetBytes("{"));
        var state = new ActorStateUnitOfWork("Counter", ActorId.Create("bad"), store, serializer);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(async () => await state.TryGetAsync<CounterState>("state"));
    }

    [Fact]
    public async Task Disabled_transport_and_error_response_paths_are_exercised()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await new DisabledSubscribeActorEventsTransport().OpenStreamAsync());

        var harness = new InMemoryTransportHarness();
        await using var provider = CoreRuntimeTestsAccess.CreateProvider(harness);
        var service = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<SubscribeActorEventsStreamManager>().Single();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        var stream = await harness.WaitForStreamAsync(cts.Token);
        _ = await stream.ReceiveAsync(cts.Token);
        await stream.SendAsync(new SubscribeActorEventsRequest("err", SubscribeActorEventsFrameKind.Invoke, "Counter", "one", "Nope", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()), cts.Token);
        var response = await stream.ReceiveAsync(cts.Token);
        await service.StopAsync(cts.Token);

        Assert.Equal("err", response.Id);
        Assert.False(response.Error);
        Assert.NotNull(response.FailureMessage);
    }

    [Fact]
    public async Task Callback_host_maps_all_callback_kinds_and_trace_headers()
    {
        var harness = new InMemoryTransportHarness();
        await using var provider = CoreRuntimeTestsAccess.CreateProvider(harness);
        var service = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<SubscribeActorEventsStreamManager>().Single();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        var stream = await harness.WaitForStreamAsync(cts.Token);
        _ = await stream.ReceiveAsync(cts.Token);

        await stream.SendAsync(new SubscribeActorEventsRequest("rem", SubscribeActorEventsFrameKind.Reminder, "Counter", "kinds", "Increment", System.Text.Encoding.UTF8.GetBytes("1"), new Dictionary<string, string> { ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", ["tracestate"] = "vendor=value" }), cts.Token);
        await stream.SendAsync(new SubscribeActorEventsRequest("timer", SubscribeActorEventsFrameKind.Timer, "Counter", "kinds", "Increment", System.Text.Encoding.UTF8.GetBytes("1"), new Dictionary<string, string>()), cts.Token);
        await stream.SendAsync(new SubscribeActorEventsRequest("deact", SubscribeActorEventsFrameKind.Deactivate, "Counter", "kinds", "ignored", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>()), cts.Token);
        var reminder = await stream.ReceiveAsync(cts.Token);
        var timer = await stream.ReceiveAsync(cts.Token);
        var deactivate = await stream.ReceiveAsync(cts.Token);
        await service.StopAsync(cts.Token);

        Assert.Equal(new[] { "deact", "rem", "timer" }, new[] { reminder.Id, timer.Id, deactivate.Id }.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Registry_proxy_registration_and_lifecycle_defensive_branches()
    {
        Assert.Throws<ArgumentException>(() => new ActorRuntimeRegistration("", typeof(ICounterActor), typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ActorRuntimeRegistration("x", null!, typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ActorRuntimeRegistration("x", typeof(ICounterActor), null!, (_, _) => new TestActor(), new CounterDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ActorRuntimeRegistration("x", typeof(ICounterActor), typeof(CounterActor), null!, new CounterDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ActorRuntimeRegistration("x", typeof(ICounterActor), typeof(CounterActor), (_, _) => new TestActor(), (Dapr.Actors.Next.Abstractions.Dispatching.IActorDispatcher)null!));
        Assert.Throws<ArgumentNullException>(() => new ActorLifecycle(null!, static (_, _) => ValueTask.CompletedTask, static (_, _, _) => ValueTask.CompletedTask, static (_, _, _, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => new ActorLifecycle(static (_, _) => ValueTask.CompletedTask, null!, static (_, _, _) => ValueTask.CompletedTask, static (_, _, _, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => new ActorLifecycle(static (_, _) => ValueTask.CompletedTask, static (_, _) => ValueTask.CompletedTask, null!, static (_, _, _, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => new ActorLifecycle(static (_, _) => ValueTask.CompletedTask, static (_, _) => ValueTask.CompletedTask, static (_, _, _) => ValueTask.CompletedTask, null!));
        var defaultLifecycle = new ActorRuntimeRegistration("x", typeof(ICounterActor), typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher()).Lifecycle;
        Assert.Same(ActorLifecycle.Empty, defaultLifecycle);

        var registry = new ActorRuntimeRegistry(Array.Empty<ActorRuntimeRegistration>(), new ServiceCollection().BuildServiceProvider());
        Assert.Throws<InvalidOperationException>(() => registry.GetByActorType("missing"));
        Assert.Throws<InvalidOperationException>(() => registry.GetAllByInterfaceType(typeof(ICounterActor)));

        Assert.Throws<ArgumentNullException>(() => ActorProxy.Configure(null!));
        ActorProxy.Reset();
        Assert.Throws<InvalidOperationException>(() => ActorProxy.Create<ICounterActor>(ActorId.Create("x"), "Counter"));
    }

    [Fact]
    public void Registry_allows_multiple_actor_types_for_the_same_interface()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var first = new ActorRuntimeRegistration("CounterA", typeof(ICounterActor), typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher());
        var second = new ActorRuntimeRegistration("CounterB", typeof(ICounterActor), typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher());
        var registry = new ActorRuntimeRegistry([first, second], services);

        Assert.Equal(["CounterA", "CounterB"], registry.ActorTypes.Order(StringComparer.Ordinal).ToArray());
        Assert.Same(first, registry.GetByActorType("CounterA"));
        Assert.Same(second, registry.GetByActorType("CounterB"));
        Assert.Equal([first, second], registry.GetAllByInterfaceType(typeof(ICounterActor)));

        var dynamicRegistration = new ActorRuntimeRegistration("CounterC", typeof(ICounterActor), typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher());
        Assert.True(registry.TryAdd(dynamicRegistration));
        Assert.False(registry.TryAdd(new ActorRuntimeRegistration("CounterC", typeof(IOtherCounterActor), typeof(CounterActor), (_, _) => new TestActor(), new CounterDispatcher())));
        Assert.Equal(3, registry.GetAllByInterfaceType(typeof(ICounterActor)).Count);

        Assert.True(registry.TryRemove("CounterB"));
        Assert.Equal(["CounterA", "CounterC"], registry.GetAllByInterfaceType(typeof(ICounterActor)).Select(registration => registration.ActorType).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Wire_serializer_handles_null_empty_and_custom_serializer()
    {
        Assert.Throws<ArgumentNullException>(() => new ActorWireSerializer(null!));
        var serializer = new ActorWireSerializer(new PlainSerializer());

        Assert.False(serializer.IsDefaultSystemTextJson);
        Assert.Empty(serializer.JsonToBytes(null));
        Assert.Null(serializer.BytesToJson(ReadOnlyMemory<byte>.Empty));
        Assert.Equal("plain", System.Text.Encoding.UTF8.GetString(serializer.SerializeToBytes("anything")));
        Assert.Equal("plain", serializer.DeserializeFromBytes<string>(System.Text.Encoding.UTF8.GetBytes("plain")));
    }

    [Fact]
    public async Task Dynamic_client_returns_null_when_runtime_returns_null()
    {
        var client = new DynamicActorClient(new NullInvocationClient(), new ActorWireSerializer(new JsonDaprSerializer()));

        Assert.Null(await client.InvokeAsync("a", "b", "c", "{}"));
    }

    [Fact]
    public async Task Activation_dispose_handles_plain_and_async_disposable_actors()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var state = new ActorStateUnitOfWork("Counter", ActorId.Create("plain"), new InMemoryActorStateStore(), new ActorWireSerializer(new JsonDaprSerializer()));
        var plain = new ActorActivation("Counter", ActorId.Create("plain"), new TestActor(), state, state, scope, ActorLifecycle.Empty);
        await plain.DisposeAsync();
        await plain.DisposeAsync();

        var asyncScope = provider.CreateAsyncScope();
        var asyncActor = new AsyncDisposableActor();
        var asyncActivation = new ActorActivation("Counter", ActorId.Create("async"), asyncActor, state, state, asyncScope, ActorLifecycle.Empty);
        await asyncActivation.DisposeAsync();

        Assert.True(asyncActor.Disposed);
    }

    [Fact]
    public async Task Runtime_deactivate_turn_and_invalid_reminder_link_are_handled()
    {
        await using var provider = CoreRuntimeTestsAccess.CreateProvider(new InMemoryTransportHarness());
        var runtime = provider.GetRequiredService<IActorRuntime>();

        await runtime.DispatchAsync(new ActorRuntimeRequest("Counter", ActorId.Create("gone"), "ignored", ActorTurnKind.Deactivate, ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), new ActorRequestContext(null, null, new Dictionary<string, string>())));
        await runtime.DispatchAsync(new ActorRuntimeRequest("Counter", ActorId.Create("badlink"), "Increment", ActorTurnKind.Reminder, System.Text.Encoding.UTF8.GetBytes("1"), new Dictionary<string, string> { ["dapr-actors-origin-traceparent"] = "bad" }, new ActorRequestContext(null, null, new Dictionary<string, string>())));
    }

    [Fact]
    public async Task Scheduler_ignores_foreign_mailbox_and_activation_provider_returns_runtime_services()
    {
        var scheduler = new ProductionActorScheduler();
        await scheduler.ScheduleAsync(new ForeignMailbox());

        var services = new ServiceCollection();
        services.AddSingleton("root");
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var state = new ActorStateUnitOfWork("Counter", ActorId.Create("a"), new InMemoryActorStateStore(), new ActorWireSerializer(new JsonDaprSerializer()));
        var context = new ActorActivationContext(ActorId.Create("a"), state);
        var activationProvider = new ActorActivationServiceProvider(scopeFactory, context);

        Assert.Same(context, activationProvider.GetService(typeof(ActorActivationContext)));
        Assert.Same(state, activationProvider.GetService(typeof(Dapr.Actors.Next.Abstractions.State.IActorStateAccessor)));
        Assert.Equal(ActorId.Create("a"), activationProvider.GetService(typeof(ActorId)));
        Assert.Equal("root", activationProvider.GetService(typeof(string)));
        await activationProvider.DisposeAsync();
    }

    [Fact]
    public async Task Core_timer_scheduler_validates_replaces_cancels_and_disposes_timers()
    {
        var scheduler = new CoreActorTimerScheduler(
            new RecordingRuntime(),
            new ActorWireSerializer(new JsonDaprSerializer()),
            TimeProvider.System);
        var actorId = ActorId.Create("timer");

        await Assert.ThrowsAsync<ArgumentException>(async () => await scheduler.ScheduleAsync("", actorId, "name", TimeSpan.FromMinutes(1), "Tick", "1"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await scheduler.ScheduleAsync("Counter", actorId, "", TimeSpan.FromMinutes(1), "Tick", "1"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await scheduler.ScheduleAsync("Counter", actorId, "name", TimeSpan.FromMinutes(1), "", "1"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await scheduler.ScheduleAsync("Counter", actorId, "name", TimeSpan.FromMilliseconds(-1), "Tick", "1"));

        await scheduler.ScheduleAsync("Counter", actorId, "name", TimeSpan.FromMinutes(1), "Tick", "1");
        await scheduler.ScheduleAsync("Counter", actorId, "name", TimeSpan.FromMinutes(1), "Tick", "2");
        await scheduler.RescheduleAsync("Counter", actorId, "name", TimeSpan.FromMinutes(1), "Tick", "3");
        await scheduler.CancelAsync("Counter", actorId, "missing");
        await scheduler.CancelAsync("Counter", actorId, "name");
        await scheduler.ScheduleAsync("Counter", actorId, "other", TimeSpan.FromMinutes(1), "Tick", "4", new Dictionary<string, string> { ["traceparent"] = "tp" });

        scheduler.Dispose();
        scheduler.Dispose();
    }

    private sealed class TestActor : IActor
    {
    }

    private sealed class AsyncDisposableActor : IActor, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NullInvocationClient : IActorInvocationClient
    {
        public Task<byte[]?> InvokeAsync(string actorType, string actorId, string methodName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);
    }

    private sealed class PlainSerializer : IDaprSerializer
    {
        public string Serialize<T>(T value) => "plain";

        public string Serialize(object? value, Type? inputType = null) => "plain";

        public T? Deserialize<T>(string? data) => (T?)(object?)data;

        public object? Deserialize(string? data, Type returnType) => data;
    }

    private sealed class NullEnvelopeWireSerializer : IActorWireSerializer
    {
        public byte[] JsonToBytes(string? json) => Array.Empty<byte>();

        public string? BytesToJson(ReadOnlyMemory<byte> bytes) => null;

        public byte[] SerializeToBytes<T>(T value) => Array.Empty<byte>();

        public T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes) => default;
    }

    private sealed class RecordingActorStateStore : IActorStateStore
    {
        private readonly InMemoryActorStateStore inner = new();

        public int WriteCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(actorType, actorId, name, cancellationToken);

        public ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            return inner.WriteAsync(actorType, actorId, name, value, cancellationToken);
        }

        public ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(actorType, actorId, name, cancellationToken);
    }

    private sealed class ForeignMailbox : IActorMailbox
    {
        public string ActorType => "Foreign";

        public ActorId ActorId => ActorId.Create("f");

        public ValueTask EnqueueAsync(ActorTurn turn, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<ActorTurn?> TryDequeueAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<ActorTurn?>(null);
    }

    private sealed class RecordingRuntime : IActorRuntime
    {
        public Task<byte[]?> InvokeAsync(string actorType, string actorId, string methodName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);

        public Task<byte[]?> DispatchAsync(ActorRuntimeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);

        public Task DeactivateAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

internal static class CoreRuntimeTestsAccess
{
    public static ServiceProvider CreateProvider(InMemoryTransportHarness harness)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new List<string>());
        services.AddScoped<ScopedProbe>();
        services.AddInMemoryActorAdapters();
        services.AddSingleton<ISubscribeActorEventsTransport>(harness);
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "Counter",
                typeof(ICounterActor),
                typeof(CounterActor),
                (sp, _) => new CounterActor(
                    sp.GetRequiredService<ActorActivationContext>(),
                    sp.GetRequiredService<IActorInvocationClient>(),
                    sp.GetRequiredService<ScopedProbe>(),
                    sp.GetRequiredService<List<string>>()),
                new CounterDispatcher());
        });
        return services.BuildServiceProvider(validateScopes: true);
    }
}
