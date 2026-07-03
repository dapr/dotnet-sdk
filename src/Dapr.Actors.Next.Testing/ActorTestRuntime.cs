using System.Runtime.CompilerServices;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// In-memory deterministic actor runtime that runs real actor factories and dispatchers without a Dapr sidecar.
/// </summary>
public sealed class ActorTestRuntime : IAsyncDisposable
{
    private readonly ServiceProvider provider;
    private readonly Dictionary<object, ActorReference> proxies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorTestRuntime"/> class.
    /// </summary>
    public ActorTestRuntime(Action<IServiceCollection>? configureServices = null, ActorTestRuntimeOptions? options = null)
    {
        Scheduler = options?.Scheduler ?? new SeededRandomActorScheduler(0);
        Time = new VirtualActorTimeProvider();
        Faults = new ActorFaults();
        StateStore = new InMemoryActorStateStore();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Time);
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<IActorTimerScheduler>(Time);
        services.AddSingleton(Faults);
        services.AddSingleton<IActorStateFaultInjector>(Faults);
        services.AddSingleton<IActorStateStore>(StateStore);
        services.AddSingleton<IActorScheduler>(Scheduler);
        services.AddSingleton<IActorInvocationClient>(sp => new ActorTestInvocationClient(sp.GetRequiredService<IActorRuntime>(), sp.GetRequiredService<ActorFaults>()));

        configureServices?.Invoke(services);

        provider = services.BuildServiceProvider(validateScopes: true);
        Runtime = provider.GetRequiredService<IActorRuntime>();
        Serializer = provider.GetRequiredService<IActorWireSerializer>();
        Time.Attach(Runtime, Serializer);

        if (provider.GetService<IActorProxyFactory>() is { } proxyFactory)
        {
            ActorProxy.Configure(proxyFactory);
        }
    }

    /// <summary>
    /// Gets the controlled scheduler.
    /// </summary>
    public ControlledActorScheduler Scheduler { get; }

    /// <summary>
    /// Gets the virtual time provider.
    /// </summary>
    public VirtualActorTimeProvider Time { get; }

    /// <summary>
    /// Gets the fault-injection controller.
    /// </summary>
    public ActorFaults Faults { get; }

    /// <summary>
    /// Gets the explored scheduling transcript.
    /// </summary>
    public IReadOnlyList<InterleavingTranscriptEntry> Transcript => Scheduler.Transcript;

    internal IActorRuntime Runtime { get; }

    internal IActorWireSerializer Serializer { get; }

    internal InMemoryActorStateStore StateStore { get; }

    /// <summary>
    /// Creates a strongly typed generated actor proxy and remembers it for introspection.
    /// </summary>
    public TActor CreateActor<TActor>(ActorId actorId, string actorType)
        where TActor : IActor
    {
        var actor = ActorProxy.Create<TActor>(actorId, actorType);
        proxies[actor!] = new ActorReference(actorType, actorId.Value);
        return actor;
    }

    /// <summary>
    /// Dispatches a weakly typed actor turn through the in-memory runtime.
    /// </summary>
    public Task<byte[]?> InvokeAsync(
        string actorType,
        ActorId actorId,
        string operationName,
        string argumentsJson = "",
        ActorTurnKind kind = ActorTurnKind.Invoke,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        Faults.BeforeInvocation(actorType, operationName);
        return Runtime.DispatchAsync(
            new ActorRuntimeRequest(
                actorType,
                actorId,
                operationName,
                kind,
                Serializer.JsonToBytes(argumentsJson),
                headers ?? new Dictionary<string, string>(StringComparer.Ordinal),
                new ActorRequestContext(null, null, new Dictionary<string, string>(StringComparer.Ordinal))),
            cancellationToken);
    }

    /// <summary>
    /// Runs scheduled turns until the runtime has no executable turns or in-flight turns left.
    /// </summary>
    public Task RunToIdle(CancellationToken cancellationToken = default) => Scheduler.RunToIdleAsync(cancellationToken);

    /// <summary>
    /// Starts one scheduled turn if an executable turn exists.
    /// </summary>
    public Task<bool> StepAsync(CancellationToken cancellationToken = default) => Scheduler.StepAsync(cancellationToken);

    /// <summary>
    /// Returns typed state accessors for a previously created generated proxy.
    /// </summary>
    public ActorStateSnapshot StateOf(object proxy)
    {
        if (!proxies.TryGetValue(proxy, out var reference))
        {
            throw new InvalidOperationException("The proxy was not created by this ActorTestRuntime.");
        }

        return new ActorStateSnapshot(this, reference.ActorType, reference.ActorId);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        ActorProxy.Reset();
        await provider.DisposeAsync().ConfigureAwait(false);
    }

    internal T? ReadState<T>(string actorType, string actorId, string name)
    {
        var bytes = StateStore.ReadAsync(actorType, actorId, name).AsTask().GetAwaiter().GetResult();
        if (bytes is null)
        {
            return default;
        }

        var envelope = Serializer.DeserializeFromBytes<ActorStateEnvelope<T>>(bytes.Value);
        return envelope is null ? default : envelope.Value;
    }

    private sealed record ActorReference(string ActorType, string ActorId);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
