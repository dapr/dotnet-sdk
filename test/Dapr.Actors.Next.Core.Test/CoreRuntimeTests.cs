using System.Diagnostics;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Observability;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Actors.Next.Core.Test;

public sealed class CoreRuntimeTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Stream_advertises_registered_actors_and_reconnects()
    {
        var harness = new InMemoryTransportHarness();
        await using var provider = CreateProvider(harness, out _);
        var service = provider.GetServices<IHostedService>().OfType<SubscribeActorEventsStreamManager>().Single();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(cts.Token);
        var first = await harness.WaitForStreamAsync(cts.Token);
        var firstAdvertisement = await first.ReceiveAsync(cts.Token);
        first.Disconnect();
        var second = await harness.WaitForStreamAsync(cts.Token);
        var secondAdvertisement = await second.ReceiveAsync(cts.Token);
        await service.StopAsync(cts.Token);

        Assert.Equal(SubscribeActorEventsFrameKind.RegisteredActors, firstAdvertisement.Kind);
        Assert.Contains("Counter", System.Text.Encoding.UTF8.GetString(firstAdvertisement.Payload.Span));
        Assert.Equal(SubscribeActorEventsFrameKind.RegisteredActors, secondAdvertisement.Kind);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Stream_manager_opens_one_stream_per_type_and_closes_dynamic_type()
    {
        var harness = new InMemoryTransportHarness();
        await using var provider = CreateMultiTypeProvider(harness);
        var service = provider.GetRequiredService<SubscribeActorEventsStreamManager>();
        var registry = provider.GetRequiredService<ActorRuntimeRegistry>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(cts.Token);
        var first = await harness.WaitForStreamAsync(cts.Token);
        var second = await harness.WaitForStreamAsync(cts.Token);
        var firstAdvertisement = await first.ReceiveAsync(cts.Token);
        var secondAdvertisement = await second.ReceiveAsync(cts.Token);
        var advertised = new[]
        {
            System.Text.Encoding.UTF8.GetString(firstAdvertisement.Payload.Span),
            System.Text.Encoding.UTF8.GetString(secondAdvertisement.Payload.Span),
        };

        var dynamic = new ActorRuntimeRegistration(
            "DynamicCounter",
            typeof(IDynamicCounterActor),
            typeof(CounterActor),
            CreateCounterActor,
            new CounterDispatcher());
        Assert.True(await service.OpenStreamForRegistrationAsync(dynamic, cts.Token));
        Assert.False(await service.OpenStreamForRegistrationAsync(dynamic, cts.Token));
        var dynamicStream = await harness.WaitForStreamAsync(cts.Token);
        var dynamicAdvertisement = await dynamicStream.ReceiveAsync(cts.Token);

        await service.OpenStreamForTypeAsync("Counter", cts.Token);
        await service.CloseStreamForTypeAsync("MissingCounter", cts.Token);
        await service.CloseStreamForTypeAsync("DynamicCounter", cts.Token);
        await service.StopAsync(cts.Token);

        Assert.Contains("Counter", advertised);
        Assert.Contains("OtherCounter", advertised);
        Assert.DoesNotContain(advertised, static value => value.Contains('\n', StringComparison.Ordinal));
        Assert.Equal("DynamicCounter", System.Text.Encoding.UTF8.GetString(dynamicAdvertisement.Payload.Span));
        Assert.DoesNotContain("DynamicCounter", registry.ActorTypes);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Stream_demultiplexes_callbacks_and_correlates_responses()
    {
        var harness = new InMemoryTransportHarness();
        await using var provider = CreateProvider(harness, out _);
        var service = provider.GetServices<IHostedService>().OfType<SubscribeActorEventsStreamManager>().Single();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(cts.Token);
        var stream = await harness.WaitForStreamAsync(cts.Token);
        _ = await stream.ReceiveAsync(cts.Token);
        await stream.SendAsync(new SubscribeActorEventsRequest("abc", SubscribeActorEventsFrameKind.Invoke, "Counter", "one", "Increment", System.Text.Encoding.UTF8.GetBytes("3"), new Dictionary<string, string>()), cts.Token);
        var response = await stream.ReceiveAsync(cts.Token);
        await service.StopAsync(cts.Token);

        Assert.Equal("abc", response.Id);
        Assert.Equal("3", System.Text.Encoding.UTF8.GetString(response.Payload.Span));
        Assert.False(response.Error);
        Assert.Null(response.FailureMessage);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Runtime_orders_turns_and_allows_same_chain_reentrancy()
    {
        await using var provider = CreateProvider(null, out _);
        var runtime = provider.GetRequiredService<IActorRuntime>();

        var calls = Enumerable.Range(0, 10)
            .Select(_ => runtime.InvokeAsync("Counter", "ordered", "Increment", System.Text.Encoding.UTF8.GetBytes("1"), new Dictionary<string, string>()))
            .ToArray();
        await Task.WhenAll(calls);
        var read = await runtime.InvokeAsync("Counter", "ordered", "Read", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>());

        var reentrant = await runtime.DispatchAsync(new ActorRuntimeRequest(
            "Counter",
            ActorId.Create("ordered"),
            "Reenter",
            ActorTurnKind.Invoke,
            ReadOnlyMemory<byte>.Empty,
            new Dictionary<string, string> { ["dapr-reentrant-id"] = "chain" },
            ActorRequestContextSnapshot.Capture()));
        var reentrantX = await runtime.DispatchAsync(new ActorRuntimeRequest(
            "Counter",
            ActorId.Create("ordered"),
            "ReenterX",
            ActorTurnKind.Invoke,
            ReadOnlyMemory<byte>.Empty,
            new Dictionary<string, string> { ["x-dapr-reentrant-id"] = "chain-x" },
            ActorRequestContextSnapshot.Capture()));

        Assert.Equal("10", System.Text.Encoding.UTF8.GetString(read!.AsSpan()));
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(reentrant!.AsSpan()));
        Assert.Equal("14", System.Text.Encoding.UTF8.GetString(reentrantX!.AsSpan()));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Production_scheduler_runs_idle_turn_inline_on_the_caller()
    {
        await using var provider = CreateBlockingProvider();
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var gate = provider.GetRequiredService<BlockingGate>();
        using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var releaser = Task.Run(async () =>
        {
            await gate.Started.Task.WaitAsync(release.Token);
            await Task.Delay(250, release.Token);
            gate.Release.Set();
        }, release.Token);

        // The production scheduler runs an idle actor's turn inline on the caller (no thread-pool hop), so a
        // synchronously blocking actor holds this call until the gate is released rather than completing on a
        // pool thread. The turn still runs correctly under the mailbox's one-turn-at-a-time guarantee.
        var call = runtime.InvokeAsync("Blocking", "one", "Wait", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>());

        Assert.Equal("1", System.Text.Encoding.UTF8.GetString((await call.WaitAsync(release.Token))!.AsSpan()));
        await releaser;
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Activation_deactivation_disposes_actor_scope_and_instance()
    {
        await using var provider = CreateProvider(null, out var created);
        var runtime = provider.GetRequiredService<IActorRuntime>();

        await runtime.InvokeAsync("Counter", "dispose", "Increment", System.Text.Encoding.UTF8.GetBytes("1"), new Dictionary<string, string>());
        var actor = Assert.Single(created);
        await runtime.DeactivateAsync("Counter", ActorId.Create("dispose"));

        Assert.True(actor.Disposed);
        Assert.True(actor.Probe.Disposed);
        Assert.Contains("deactivate", actor.Events);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task State_cache_flushes_plain_state_and_reloads_on_activation()
    {
        await using var provider = CreateProvider(null, out _);
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var store = provider.GetRequiredService<IActorStateStore>();
        var serializer = provider.GetRequiredService<IActorWireSerializer>();

        await runtime.InvokeAsync("Counter", "stateful", "Increment", System.Text.Encoding.UTF8.GetBytes("5"), new Dictionary<string, string>());
        var bytes = await store.ReadAsync("Counter", "stateful", "state");
        var envelope = serializer.DeserializeFromBytes<ActorStatePlainEnvelope<CounterState>>(bytes!.Value);
        await runtime.DeactivateAsync("Counter", ActorId.Create("stateful"));
        var read = await runtime.InvokeAsync("Counter", "stateful", "Read", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>());

        Assert.Equal(ActorStateFormKind.Plain, envelope!.Header.FormKind);
        Assert.Equal(5, envelope.Value.Value);
        Assert.Equal("5", System.Text.Encoding.UTF8.GetString(read!.AsSpan()));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Serializer_adapter_owns_utf8_transcode()
    {
        var serializer = new ActorWireSerializer(new Dapr.Common.Serialization.JsonDaprSerializer());

        var bytes = serializer.SerializeToBytes(new ActorStateEnvelope<int>(
            ActorStateEnvelopeHeader.Create(ActorStateFormKind.Enveloped, serializer.SerializerId, serializer.SerializerVersion),
            new ActorStateDiscriminator(7, "h1:test"),
            42));
        var envelope = serializer.DeserializeFromBytes<ActorStateEnvelope<int>>(bytes);
        var json = serializer.BytesToJson(serializer.JsonToBytes("""{"x":1}"""));

        Assert.True(serializer.IsDefaultSystemTextJson);
        Assert.Equal(7, envelope!.Discriminator.ChainIndex);
        Assert.Equal(42, envelope.Value);
        Assert.Equal("""{"x":1}""", json);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Dynamic_client_routes_through_runtime()
    {
        await using var provider = CreateProvider(null, out _);
        var client = provider.GetRequiredService<IDynamicActorClient>();

        var result = await client.InvokeAsync("Counter", "dynamic", "Increment", "4");

        Assert.Equal("4", result);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Actor_proxy_uses_generated_factory_seam()
    {
        var expected = new TestProxy();
        ActorProxy.Configure(new TestProxyFactory(expected));

        var proxy = ActorProxy.Create<ICounterActor>(ActorId.Create("p1"), "Counter");

        Assert.Same(expected, proxy);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Filter_pipeline_and_lifecycle_hooks_run_in_order()
    {
        var log = new List<string>();
        await using var provider = CreateProvider(null, out _, log);
        var runtime = provider.GetRequiredService<IActorRuntime>();

        await runtime.InvokeAsync("Counter", "pipeline", "Increment", System.Text.Encoding.UTF8.GetBytes("1"), new Dictionary<string, string>());

        Assert.Equal(new[] { "activate", "filter-before", "pre", "method", "filter-after", "post" }, log);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Actor_request_context_snapshots_and_restores_activity_baggage()
    {
        using var source = new ActivitySource("test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("parent");
        activity!.AddBaggage("tenant", "t1");
        var snapshot = ActorRequestContextSnapshot.Capture();

        using (ActorRequestContextSnapshot.Restore(snapshot))
        {
            Assert.Equal("t1", Activity.Current!.GetBaggageItem("tenant"));
        }
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Reminder_turn_adds_span_link_from_origin_traceparent()
    {
        using var listener = new RecordingActivityListener("Dapr.Actors.Next.Core");
        await using var provider = CreateProvider(null, out _);
        var runtime = provider.GetRequiredService<IActorRuntime>();
        var traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        await runtime.DispatchAsync(new ActorRuntimeRequest(
            "Counter",
            ActorId.Create("reminder"),
            "Increment",
            ActorTurnKind.Reminder,
            System.Text.Encoding.UTF8.GetBytes("1"),
            new Dictionary<string, string> { ["dapr-actors-origin-traceparent"] = traceParent },
            new ActorRequestContext(null, null, new Dictionary<string, string>())));

        Assert.Contains(listener.Started, activity => activity.Links.Any());
    }

    private static ServiceProvider CreateProvider(InMemoryTransportHarness? harness, out List<CounterActor> created, List<string>? log = null)
    {
        created = new List<CounterActor>();
        var createdActors = created;
        var eventLog = log ?? new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(eventLog);
        services.AddScoped<ScopedProbe>();
        services.AddSingleton<IActorTurnFilter>(new RecordingFilter(eventLog));
        services.AddInMemoryActorAdapters();
        if (harness is not null)
        {
            services.AddSingleton<ISubscribeActorEventsTransport>(harness);
        }

        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "Counter",
                typeof(ICounterActor),
                typeof(CounterActor),
                (sp, _) =>
                {
                    var actor = new CounterActor(
                        sp.GetRequiredService<ActorActivationContext>(),
                        sp.GetRequiredService<IActorInvocationClient>(),
                        sp.GetRequiredService<ScopedProbe>(),
                        sp.GetRequiredService<List<string>>());
                    createdActors.Add(actor);
                    return actor;
                },
                new CounterDispatcher(),
                new ActorLifecycle(
                    static (actor, ct) => ((CounterActor)actor).ActivateAsync(ct),
                    static (actor, ct) => ((CounterActor)actor).DeactivateAsync(ct),
                    static (actor, context, ct) => ((CounterActor)actor).PreAsync(context, ct),
                    static (actor, context, exception, ct) => ((CounterActor)actor).PostAsync(context, exception, ct)));
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider CreateBlockingProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<BlockingGate>();
        services.AddInMemoryActorAdapters();
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "Blocking",
                typeof(IBlockingActor),
                typeof(BlockingActor),
                static (sp, actorId) => new BlockingActor(actorId, sp.GetRequiredService<BlockingGate>()),
                new BlockingDispatcher());
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider CreateMultiTypeProvider(InMemoryTransportHarness harness)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new List<string>());
        services.AddScoped<ScopedProbe>();
        services.AddInMemoryActorAdapters();
        services.AddSingleton<ISubscribeActorEventsTransport>(harness);
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add("Counter", typeof(ICounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
            registrations.Add("OtherCounter", typeof(IOtherCounterActor), typeof(CounterActor), CreateCounterActor, new CounterDispatcher());
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IActor CreateCounterActor(IServiceProvider sp, ActorId _) =>
        new CounterActor(
            sp.GetRequiredService<ActorActivationContext>(),
            sp.GetRequiredService<IActorInvocationClient>(),
            sp.GetRequiredService<ScopedProbe>(),
            sp.GetRequiredService<List<string>>());

    private sealed class TestProxy : ICounterActor
    {
    }

    private sealed class TestProxyFactory : IActorProxyFactory
    {
        private readonly ICounterActor proxy;

        public TestProxyFactory(ICounterActor proxy)
        {
            this.proxy = proxy;
        }

        public TActor Create<TActor>(ActorId actorId, string actorType)
            where TActor : IActor => (TActor)proxy;
    }

    private interface IBlockingActor : IActor
    {
    }

    private sealed class BlockingGate
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ManualResetEventSlim Release { get; } = new(false);
    }

    private sealed class BlockingActor(ActorId actorId, BlockingGate gate) : Actor
    {
        protected override ActorId Id { get; } = actorId;

        protected override IActorStateAccessor State => throw new NotSupportedException();

        public int Wait(CancellationToken cancellationToken)
        {
            gate.Started.TrySetResult();
            gate.Release.Wait(cancellationToken);
            return 1;
        }
    }

    private sealed class BlockingDispatcher : IActorDispatcher
    {
        public ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default) =>
            new(new ActorDispatchResponse(System.Text.Encoding.UTF8.GetBytes(((BlockingActor)actor).Wait(cancellationToken).ToString())));
    }

    private sealed class RecordingActivityListener : IDisposable
    {
        private readonly ActivityListener listener;

        public RecordingActivityListener(string sourceName)
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => started.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);
        }

        private readonly System.Collections.Concurrent.ConcurrentBag<Activity> started = new();

        public Activity[] Started => started.ToArray();

        public void Dispose() => listener.Dispose();
    }
}
