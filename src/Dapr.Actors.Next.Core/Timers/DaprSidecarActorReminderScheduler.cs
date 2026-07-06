using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Transport;
using Google.Protobuf;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Schedules durable actor reminders through the sidecar so callbacks return over the actor event stream.
/// </summary>
public sealed class DaprSidecarActorReminderScheduler : IActorReminderScheduler
{
    private readonly Lazy<P.Dapr.DaprClient> client;
    private readonly IActorWireSerializer serializer;
    private readonly string? daprApiToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprSidecarActorReminderScheduler"/> class.
    /// </summary>
    public DaprSidecarActorReminderScheduler(P.Dapr.DaprClient client, IActorWireSerializer serializer, string? daprApiToken = null)
        : this(CreateEagerAccessor(client), serializer, daprApiToken)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprSidecarActorReminderScheduler"/> class whose Dapr gRPC
    /// client is resolved on first use, so constructing the scheduler does not eagerly build the transport channel.
    /// </summary>
    public DaprSidecarActorReminderScheduler(Lazy<P.Dapr.DaprClient> client, IActorWireSerializer serializer, string? daprApiToken = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.daprApiToken = daprApiToken;
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        string argumentsJson,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        if (period < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period cannot be negative.");
        }

        if (ttl.HasValue && ttl.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL cannot be negative.");
        }

        var request = new P.RegisterActorReminderRequest
        {
            ActorType = actorType,
            ActorId = actorId.Value,
            Name = name,
            DueTime = ActorScheduleDurationFormatter.Format(dueTime),
            Period = ActorScheduleDurationFormatter.Format(period),
            Data = ByteString.CopyFrom(serializer.JsonToBytes(argumentsJson)),
        };

        if (ttl.HasValue)
        {
            request.Ttl = ActorScheduleDurationFormatter.Format(ttl.Value);
        }

        if (overwrite.HasValue)
        {
            request.Overwrite = overwrite.Value;
        }

        await client.Value.RegisterActorReminderAsync(
            request,
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await client.Value.UnregisterActorReminderAsync(
            new P.UnregisterActorReminderRequest { ActorType = actorType, ActorId = actorId.Value, Name = name },
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    private static Lazy<P.Dapr.DaprClient> CreateEagerAccessor(P.Dapr.DaprClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new Lazy<P.Dapr.DaprClient>(() => client);
    }
}
