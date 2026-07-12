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

using System.Runtime.CompilerServices;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
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
        services.AddSingleton<IActorReminderScheduler>(Time);
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
    /// Seeds a named actor state value directly into the in-memory store.
    /// </summary>
    public ValueTask SeedStateAsync<T>(
        string actorType,
        ActorId actorId,
        string name,
        T value,
        ActorStateSeedForm form = ActorStateSeedForm.Enveloped,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        return SeedStateAsync(actorType, actorId.Value, name, value, form, cancellationToken);
    }

    /// <summary>
    /// Seeds a named actor state value directly into the in-memory store.
    /// </summary>
    public async ValueTask SeedStateAsync<T>(
        string actorType,
        string actorId,
        string name,
        T value,
        ActorStateSeedForm form = ActorStateSeedForm.Enveloped,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var bytes = form switch
        {
            ActorStateSeedForm.Enveloped => CreateEnvelopedSeed(value),
            ActorStateSeedForm.Plain => Serializer.SerializeToBytes(new ActorStatePlainEnvelope<T>(
                ActorStateEnvelopeHeader.Create(ActorStateFormKind.Plain, Serializer.SerializerId, Serializer.SerializerVersion),
                value)),
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, "Unsupported actor state seed form."),
        };

        await StateStore.WriteAsync(actorType, actorId, name, bytes, cancellationToken).ConfigureAwait(false);
    }

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

    /// <summary>
    /// Returns typed state accessors for a known actor reference.
    /// </summary>
    public ActorStateSnapshot StateOf(string actorType, ActorId actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        return new ActorStateSnapshot(this, actorType, actorId.Value);
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

        if (TryPeekForm(bytes.Value, out var formKind))
        {
            switch (formKind)
            {
                case ActorStateFormKind.Enveloped:
                {
                    var enrolled = Serializer.DeserializeFromBytes<ActorStateEnvelope<T>>(bytes.Value);
                    return enrolled is null ? default : enrolled.Value;
                }

                case ActorStateFormKind.Plain:
                {
                    var plain = Serializer.DeserializeFromBytes<ActorStatePlainEnvelope<T>>(bytes.Value);
                    return plain is null ? default : plain.Value;
                }

                default:
                    throw new InvalidOperationException($"State '{name}' has unsupported actor state form '{formKind}'.");
            }
        }

        var value = Serializer.DeserializeFromBytes<T>(bytes.Value);
        if (value is not null)
        {
            return value;
        }

        return Serializer.DeserializeFromBytes<LegacyActorStateEnvelope<T>>(bytes.Value) is { } envelope
            ? envelope.Value
            : default;
    }

    private byte[] CreateEnvelopedSeed<T>(T value)
    {
        var migrator = provider.GetRequiredService<IActorStateMigrator>();
        var node = migrator.ResolveTargetNode(typeof(T))
            ?? throw new InvalidOperationException($"Actor state type '{typeof(T).FullName}' is not registered for migration.");

        var envelope = new ActorStateEnvelope<T>(
            ActorStateEnvelopeHeader.Create(ActorStateFormKind.Enveloped, Serializer.SerializerId, Serializer.SerializerVersion),
            new ActorStateDiscriminator(node.Index, node.ShapeHash),
            value);
        return Serializer.SerializeToBytes(envelope);
    }

    private static bool TryPeekForm(ReadOnlyMemory<byte> bytes, out ActorStateFormKind formKind)
    {
        formKind = default;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !TryGetProperty(document.RootElement, nameof(ActorStateEnvelope<object>.Header), out var headerElement)
                || headerElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !TryReadByte(headerElement, nameof(ActorStateEnvelopeHeader.Magic), out var magic)
                || magic != ActorStateEnvelopeHeader.CurrentMagic)
            {
                return false;
            }

            formKind = ReadFormKind(GetProperty(headerElement, nameof(ActorStateEnvelopeHeader.FormKind)));
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool TryReadByte(System.Text.Json.JsonElement element, string propertyName, out byte value)
    {
        value = default;
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == System.Text.Json.JsonValueKind.Number && property.TryGetByte(out value))
        {
            return true;
        }

        if (property.ValueKind == System.Text.Json.JsonValueKind.String && byte.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static System.Text.Json.JsonElement GetProperty(System.Text.Json.JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property)
            ? property
            : throw new KeyNotFoundException(propertyName);

    private static bool TryGetProperty(System.Text.Json.JsonElement element, string propertyName, out System.Text.Json.JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        var camelCase = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        return element.TryGetProperty(camelCase, out property);
    }

    private static ActorStateFormKind ReadFormKind(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            return (ActorStateFormKind)element.GetInt32();
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.String && Enum.TryParse<ActorStateFormKind>(element.GetString(), ignoreCase: false, out var formKind))
        {
            return formKind;
        }

        throw new InvalidOperationException("Actor state header has an invalid form kind.");
    }

    private sealed record LegacyActorStateEnvelope<T>(int SchemaVersion, T Value);

    private sealed record ActorReference(string ActorType, string ActorId);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
