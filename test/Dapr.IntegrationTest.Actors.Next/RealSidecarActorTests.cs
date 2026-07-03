using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Core.Transport;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Containers.Dapr;
using Dapr.Testcontainers.Harnesses;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using P = Dapr.Client.Autogen.Grpc.v1;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dapr.IntegrationTest.Actors.Next;

/// <summary>
/// Real-sidecar integration tests for the Actors Next app-callback stream.
/// </summary>
public sealed class RealSidecarActorTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);
    private DaprTestEnvironment? environment;
    private DaprdContainer? daprd;
    private WebApplication? app;
    private P.Dapr.DaprClient? client;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        Probe.Reset();
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actors-next-components");
        var appPort = PortUtilities.GetAvailablePort();
        var daprHttpPort = PortUtilities.GetAvailablePort();
        var daprGrpcPort = PortUtilities.GetAvailablePort();
        while (daprHttpPort == appPort)
        {
            daprHttpPort = PortUtilities.GetAvailablePort();
        }

        while (daprGrpcPort == appPort || daprGrpcPort == daprHttpPort)
        {
            daprGrpcPort = PortUtilities.GetAvailablePort();
        }

        var options = new DaprRuntimeOptions("1.18.0")
            .WithAppId($"actors-next-{Guid.NewGuid():N}")
            .WithAppProtocol("grpc");
        options.AppPort = appPort;

        try
        {
            environment = new DaprTestEnvironment(options, needsActorState: true);
            await environment.StartAsync();
            RedisContainer.Yaml.WriteStateStoreYamlToFolder(
                componentsDir,
                redisHost: $"{environment.RedisContainer!.NetworkAlias}:{RedisContainer.ContainerPort}");
            WriteActorConfigYaml(componentsDir);

            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_HTTP_ENDPOINT"] = $"http://127.0.0.1:{daprHttpPort}",
                ["DAPR_GRPC_ENDPOINT"] = $"http://127.0.0.1:{daprGrpcPort}",
            });
            builder.WebHost.ConfigureKestrel(server =>
            {
                server.ListenLocalhost(appPort, listen => listen.Protocols = HttpProtocols.Http2);
            });
            ConfigureActorHost(builder);
            app = builder.Build();
            await app.StartAsync();

            daprd = new DaprdContainer(
                options.AppId,
                componentsDir,
                options,
                environment.Network,
                new HostPortPair(environment.PlacementAlias, DaprPlacementContainer.InternalPort),
                new HostPortPair(environment.SchedulerAlias, DaprSchedulerContainer.InternalPort),
                daprHttpPort,
                daprGrpcPort,
                configFilePath: "/components/actor-config.yaml");
            await daprd.StartAsync();
        }
        catch
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }

            if (daprd is not null)
            {
                await daprd.DisposeAsync();
            }

            if (environment is not null)
            {
                await environment.DisposeAsync();
            }

            throw;
        }

        client = app.Services.CreateScope().ServiceProvider.GetRequiredService<P.Dapr.DaprClient>();
        await WaitForActorRuntimeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.DisposeAsync();
        }

        if (daprd is not null)
        {
            await daprd.DisposeAsync();
        }

        if (environment is not null)
        {
            await environment.DisposeAsync();
        }
    }

    private static void ConfigureActorHost(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(_ =>
        {
            var endpoint = builder.Configuration["DAPR_GRPC_ENDPOINT"] ?? throw new InvalidOperationException("Missing Dapr gRPC endpoint.");
            return new P.Dapr.DaprClient(GrpcChannel.ForAddress(endpoint));
        });
        builder.Services.AddSingleton<ISubscribeActorEventsTransport>(sp =>
            new RecordingActorEventsTransport(sp.GetRequiredService<P.Dapr.DaprClient>()));
        builder.Services.AddSingleton<Probe>();
        builder.Services.AddDaprActors(options =>
        {
            options.ActorIdleTimeout = TimeSpan.FromSeconds(2);
            options.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(1);
            options.EnableReentrancy = true;
            options.MaxReentrantDepth = 8;
        });
        builder.Services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "ProtocolActor",
                typeof(IProtocolActor),
                typeof(ProtocolActor),
                static (sp, _) => new ProtocolActor(
                    sp.GetRequiredService<ActorActivationContext>(),
                    sp.GetRequiredService<IActorTimerScheduler>(),
                    sp.GetRequiredService<IActorRuntime>(),
                    sp.GetRequiredService<IActorStateStore>(),
                    sp.GetRequiredService<IActorWireSerializer>(),
                    sp.GetRequiredService<Probe>()),
                new ProtocolActorDispatcher(),
                new ActorLifecycle(
                    static (actor, ct) => ((ProtocolActor)actor).ActivateAsync(ct),
                    static (actor, ct) => ((ProtocolActor)actor).DeactivateAsync(ct),
                    static (_, _, _) => ValueTask.CompletedTask,
                    static (_, _, _, _) => ValueTask.CompletedTask));
        });
    }

    private static void WriteActorConfigYaml(string componentsDirectory)
    {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: actorConfig
            spec:
              features:
                - name: "ActorStateTTL"
                  enabled: true
            """;
        Directory.CreateDirectory(componentsDirectory);
        File.WriteAllText(Path.Combine(componentsDirectory, "actor-config.yaml"), yaml);
    }

    /// <summary>
    /// Verifies real actor invoke serialization and request metadata over the sidecar stream.
    /// </summary>
    [Fact]
    public async Task Invoke_round_trips_payload_content_type_and_metadata()
    {
        using var cts = new CancellationTokenSource(Timeout);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new ProtocolRequest("alpha", 7));
        var response = await client!.InvokeActorAsync(CreateInvoke("ProtocolActor", "roundtrip", "Echo", payload, new()
        {
            ["content-type"] = "application/json",
            ["x-test"] = "metadata",
        }), cancellationToken: cts.Token);

        var result = JsonSerializer.Deserialize<ProtocolResponse>(response.Data.Span)!;

        Assert.Equal("alpha", result.Text);
        Assert.Equal(7, result.Number);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("metadata", result.Metadata);
    }

    /// <summary>
    /// Verifies scheduler-backed reminder callbacks and sidecar-backed timer callbacks.
    /// </summary>
    [Fact]
    public async Task Reminder_and_timer_callbacks_fire_through_real_sidecar()
    {
        using var cts = new CancellationTokenSource(Timeout);
        var actorId = $"callbacks-{Guid.NewGuid():N}";

        await client!.RegisterActorReminderAsync(new P.RegisterActorReminderRequest
        {
            ActorType = "ProtocolActor",
            ActorId = actorId,
            Name = "reminder",
            DueTime = "0ms",
            Period = "100ms",
            Data = ByteString.CopyFromUtf8("\"from-reminder\""),
            Overwrite = true,
        }, cancellationToken: cts.Token);

        await client.InvokeActorAsync(CreateInvoke("ProtocolActor", actorId, "ScheduleTimer", Encoding.UTF8.GetBytes("\"from-timer\"")), cancellationToken: cts.Token);

        await WaitUntilAsync(
            () => Probe.Reminders.Contains(actorId) && Probe.Timers.Contains(actorId),
            cts.Token,
            () => $"reminders=[{string.Join(",", Probe.Reminders)}], timers=[{string.Join(",", Probe.Timers)}], streamErrors=[{string.Join(" | ", Probe.StreamErrors.Select(static ex => ex.Message))}]");
    }

    /// <summary>
    /// Verifies idle deactivation is delivered over the real app-callback stream.
    /// </summary>
    [Fact]
    public async Task Idle_deactivation_is_delivered()
    {
        using var cts = new CancellationTokenSource(Timeout);
        var actorId = $"idle-{Guid.NewGuid():N}";

        await client!.InvokeActorAsync(CreateInvoke("ProtocolActor", actorId, "Echo", JsonSerializer.SerializeToUtf8Bytes(new ProtocolRequest("idle", 1))), cancellationToken: cts.Token);

        await WaitUntilAsync(() => Probe.Deactivated.Contains(actorId), cts.Token, () => $"deactivated=[{string.Join(",", Probe.Deactivated)}]");
    }

    /// <summary>
    /// Verifies actor state persisted in an older envelope is upcasted on a later activation.
    /// </summary>
    [Fact]
    public async Task Legacy_state_envelope_is_upcasted_on_activation()
    {
        using var cts = new CancellationTokenSource(Timeout);
        var actorId = $"state-{Guid.NewGuid():N}";

        await client!.InvokeActorAsync(CreateInvoke("ProtocolActor", actorId, "SeedLegacy", Encoding.UTF8.GetBytes("\"Ada\"")), cancellationToken: cts.Token);
        await client.InvokeActorAsync(CreateInvoke("ProtocolActor", actorId, "Deactivate", ReadOnlyMemory<byte>.Empty), cancellationToken: cts.Token);
        var response = await client.InvokeActorAsync(CreateInvoke("ProtocolActor", actorId, "ReadCurrent", ReadOnlyMemory<byte>.Empty), cancellationToken: cts.Token);
        var current = JsonSerializer.Deserialize<CurrentProfile>(response.Data.Span)!;

        Assert.Equal("Ada Lovelace", current.FullName);
        Assert.Equal(2, current.SchemaVersion);
    }

    private static P.InvokeActorRequest CreateInvoke(
        string actorType,
        string actorId,
        string method,
        ReadOnlyMemory<byte> data,
        Dictionary<string, string>? metadata = null)
    {
        var request = new P.InvokeActorRequest
        {
            ActorType = actorType,
            ActorId = actorId,
            Method = method,
            Data = ByteString.CopyFrom(data.Span),
        };

        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                request.Metadata[key] = value;
            }
        }

        return request;
    }

    private async Task WaitForActorRuntimeAsync()
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (true)
        {
            if (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Actor runtime did not become ready. Advertisements=[{string.Join(",", Probe.Advertisements)}]. StreamErrors=[{string.Join(" | ", Probe.StreamErrors.Select(static ex => ex.Message))}].");
            }

            try
            {
                await client!.InvokeActorAsync(CreateInvoke("ProtocolActor", "ready", "Ping", ReadOnlyMemory<byte>.Empty), cancellationToken: cts.Token);
                return;
            }
            catch (RpcException) when (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken, Func<string> failure)
    {
        while (!predicate())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(failure());
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
    }
}

/// <summary>
/// Marker interface for the integration actor registration.
/// </summary>
public interface IProtocolActor : IActor
{
}

/// <summary>
/// Request DTO used by protocol round-trip tests.
/// </summary>
public sealed record ProtocolRequest(string Text, int Number);

/// <summary>
/// Response DTO used by protocol round-trip tests.
/// </summary>
public sealed record ProtocolResponse(string Text, int Number, string? ContentType, string? Metadata);

/// <summary>
/// Legacy state DTO persisted with schema version one.
/// </summary>
public sealed record LegacyProfile(string Name);

/// <summary>
/// Current state DTO persisted with schema version two.
/// </summary>
public sealed record CurrentProfile(string FullName, int SchemaVersion);

/// <summary>
/// Shared observable probe for sidecar callbacks.
/// </summary>
public sealed class Probe
{
    /// <summary>
    /// Gets actor ids that received reminder callbacks.
    /// </summary>
    public static ConcurrentBag<string> Reminders { get; } = [];

    /// <summary>
    /// Gets actor ids that received timer callbacks.
    /// </summary>
    public static ConcurrentBag<string> Timers { get; } = [];

    /// <summary>
    /// Gets actor ids that were deactivated.
    /// </summary>
    public static ConcurrentBag<string> Deactivated { get; } = [];

    /// <summary>
    /// Gets actor event stream errors.
    /// </summary>
    public static ConcurrentBag<Exception> StreamErrors { get; } = [];

    /// <summary>
    /// Gets actor type advertisement payloads written to the event stream.
    /// </summary>
    public static ConcurrentBag<string> Advertisements { get; } = [];

    /// <summary>
    /// Clears all probe observations.
    /// </summary>
    public static void Reset()
    {
        while (Reminders.TryTake(out _))
        {
        }

        while (Timers.TryTake(out _))
        {
        }

        while (Deactivated.TryTake(out _))
        {
        }

        while (StreamErrors.TryTake(out _))
        {
        }

        while (Advertisements.TryTake(out _))
        {
        }
    }
}

/// <summary>
/// Test transport wrapper that records stream registration behavior.
/// </summary>
public sealed class RecordingActorEventsTransport(P.Dapr.DaprClient client) : ISubscribeActorEventsTransport
{
    private readonly DaprActorEventsTransport inner = new(client);

    /// <inheritdoc />
    public async ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return new Stream(await inner.OpenStreamAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            Probe.StreamErrors.Add(ex);
            throw;
        }
    }

    private sealed class Stream(ISubscribeActorEventsStream inner) : ISubscribeActorEventsStream
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IAsyncEnumerator<SubscribeActorEventsRequest>? enumerator = null;
            try
            {
                enumerator = inner.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Probe.StreamErrors.Add(ex);
                        throw;
                    }

                    if (!moved)
                    {
                        yield break;
                    }

                    yield return enumerator.Current;
                }
            }
            finally
            {
                if (enumerator is not null)
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default)
        {
            if (response.Kind == SubscribeActorEventsFrameKind.RegisteredActors)
            {
                Probe.Advertisements.Add(Encoding.UTF8.GetString(response.Payload.Span));
            }

            try
            {
                await inner.WriteAsync(response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Probe.StreamErrors.Add(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}

/// <summary>
/// Integration actor hosted over the Actors Next app-callback stream.
/// </summary>
public sealed class ProtocolActor(
    ActorActivationContext context,
    IActorTimerScheduler timerScheduler,
    IActorRuntime runtime,
    IActorStateStore stateStore,
    IActorWireSerializer serializer,
    Probe probe) : Actor
{
    /// <inheritdoc />
    protected override ActorId Id => context.ActorId;

    /// <inheritdoc />
    protected override IActorStateAccessor State => context.State;

    /// <summary>
    /// Completes activation and upcasts legacy persisted state if present.
    /// </summary>
    public async ValueTask ActivateAsync(CancellationToken cancellationToken)
    {
        var raw = await stateStore.ReadAsync("ProtocolActor", Id.Value, "profile", cancellationToken);
        if (raw is null)
        {
            return;
        }

        var legacy = serializer.DeserializeFromBytes<ActorStateEnvelope<LegacyProfile>>(raw.Value);
        if (legacy?.SchemaVersion == 1)
        {
            await State.SetAsync("profile", new CurrentProfile($"{legacy.Value.Name} Lovelace", 2), 2, cancellationToken);
        }
    }

    /// <summary>
    /// Records deactivation callbacks.
    /// </summary>
    public ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        _ = probe;
        Probe.Deactivated.Add(Id.Value);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Health probe method.
    /// </summary>
    public Task PingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Echoes the request and selected request metadata.
    /// </summary>
    public Task<ProtocolResponse> EchoAsync(ProtocolRequest request, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken) =>
        Task.FromResult(new ProtocolResponse(
            request.Text,
            request.Number,
            headers.GetValueOrDefault("content-type"),
            headers.GetValueOrDefault("x-test")));

    /// <summary>
    /// Schedules a real Dapr actor timer.
    /// </summary>
    public async Task ScheduleTimerAsync(string value, CancellationToken cancellationToken)
    {
        await timerScheduler.ScheduleAsync("ProtocolActor", Id, "timer", TimeSpan.FromMilliseconds(100), "TimerFired", JsonSerializer.Serialize(value), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Handles a real Dapr timer callback.
    /// </summary>
    public Task TimerFiredAsync(CancellationToken cancellationToken)
    {
        Probe.Timers.Add(Id.Value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles a real Dapr reminder callback.
    /// </summary>
    public Task ReminderAsync(CancellationToken cancellationToken)
    {
        Probe.Reminders.Add(Id.Value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Persists a legacy state envelope.
    /// </summary>
    public async Task SeedLegacyAsync(string name, CancellationToken cancellationToken)
    {
        await State.SetAsync("profile", new LegacyProfile(name), 1, cancellationToken);
    }

    /// <summary>
    /// Reads the current state envelope after activation.
    /// </summary>
    public async Task<CurrentProfile> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("profile", static () => new CurrentProfile(string.Empty, 2), cancellationToken);
        return state.Value;
    }

    /// <summary>
    /// Forces deactivation through the runtime.
    /// </summary>
    public Task DeactivateAsync() => Task.CompletedTask;

    /// <summary>
    /// Forces this activation out of the runtime cache.
    /// </summary>
    public Task ForceDeactivateAsync(CancellationToken cancellationToken) =>
        runtime.DeactivateAsync("ProtocolActor", Id, cancellationToken);
}

/// <summary>
/// Hand-written dispatcher used to keep the integration test independent from generated code.
/// </summary>
public sealed class ProtocolActorDispatcher : IActorDispatcher
{
    /// <inheritdoc />
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var protocol = (ProtocolActor)actor;
        return request.MethodName switch
        {
            "Ping" => await CompleteAsync(protocol.PingAsync(cancellationToken)),
            "Echo" => new ActorDispatchResponse(JsonSerializer.SerializeToUtf8Bytes(await protocol.EchoAsync(JsonSerializer.Deserialize<ProtocolRequest>(request.Payload.Span)!, request.Headers, cancellationToken))),
            "ScheduleTimer" => await CompleteAsync(protocol.ScheduleTimerAsync(JsonSerializer.Deserialize<string>(request.Payload.Span)!, cancellationToken)),
            "TimerFired" => await CompleteAsync(protocol.TimerFiredAsync(cancellationToken)),
            "reminder" => await CompleteAsync(protocol.ReminderAsync(cancellationToken)),
            "SeedLegacy" => await CompleteAsync(protocol.SeedLegacyAsync(JsonSerializer.Deserialize<string>(request.Payload.Span)!, cancellationToken)),
            "ReadCurrent" => new ActorDispatchResponse(JsonSerializer.SerializeToUtf8Bytes(await protocol.ReadCurrentAsync(cancellationToken))),
            "Deactivate" => await CompleteAsync(protocol.ForceDeactivateAsync(cancellationToken)),
            _ => throw new InvalidOperationException($"Unknown method '{request.MethodName}'."),
        };
    }

    private static async ValueTask<ActorDispatchResponse> CompleteAsync(Task task)
    {
        await task.ConfigureAwait(false);
        return new ActorDispatchResponse(null);
    }
}
