using System.Globalization;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Transport;
using Google.Protobuf;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Schedules actor timers through the sidecar so callbacks return over the actor event stream.
/// </summary>
public sealed class DaprSidecarActorTimerScheduler : IActorTimerScheduler
{
    private readonly Lazy<P.Dapr.DaprClient> client;
    private readonly IActorWireSerializer serializer;
    private readonly string? daprApiToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprSidecarActorTimerScheduler"/> class.
    /// </summary>
    public DaprSidecarActorTimerScheduler(P.Dapr.DaprClient client, IActorWireSerializer serializer, string? daprApiToken = null)
        : this(CreateEagerAccessor(client), serializer, daprApiToken)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprSidecarActorTimerScheduler"/> class whose Dapr gRPC
    /// client is resolved on first use, so constructing the scheduler does not eagerly build the transport channel.
    /// </summary>
    public DaprSidecarActorTimerScheduler(Lazy<P.Dapr.DaprClient> client, IActorWireSerializer serializer, string? daprApiToken = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.daprApiToken = daprApiToken;
    }

    private static Lazy<P.Dapr.DaprClient> CreateEagerAccessor(P.Dapr.DaprClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new Lazy<P.Dapr.DaprClient>(() => client);
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        await client.Value.RegisterActorTimerAsync(
            new P.RegisterActorTimerRequest
            {
                ActorType = actorType,
                ActorId = actorId.Value,
                Name = name,
                DueTime = FormatDuration(dueTime),
                Period = FormatDuration(TimeSpan.FromMilliseconds(100)),
                Callback = operationName,
                Data = ByteString.CopyFrom(serializer.JsonToBytes(argumentsJson)),
            },
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, argumentsJson, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        await client.Value.UnregisterActorTimerAsync(
            new P.UnregisterActorTimerRequest { ActorType = actorType, ActorId = actorId.Value, Name = name },
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
        {
            return "0ms";
        }

        return duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms";
    }
}
