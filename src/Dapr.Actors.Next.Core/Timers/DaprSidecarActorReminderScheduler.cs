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
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Transport;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
        await ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            serializer.JsonToBytes(argumentsJson),
            ttl,
            overwrite,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        await ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            arguments,
            ttl,
            overwrite,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        TArguments arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default)
    {
        await ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            serializer.SerializeToBytes(arguments),
            ttl,
            overwrite,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ScheduleCoreAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        TimeSpan? ttl,
        bool? overwrite,
        CancellationToken cancellationToken)
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
            Data = ByteString.CopyFrom(arguments),
        };

        if (ttl.HasValue)
        {
            request.Ttl = ActorScheduleDurationFormatter.Format(ttl.Value);
        }

        if (overwrite.HasValue)
        {
            request.Overwrite = overwrite.Value;
        }

        try
        {
            await client.Value.RegisterActorReminderAsync(
                request,
                DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            throw new ActorReminderAlreadyExistsException(actorType, actorId.Value, name, ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ActorReminderInfo?> GetAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            var response = await client.Value.GetActorReminderAsync(
                new P.GetActorReminderRequest { ActorType = actorType, ActorId = actorId.Value, Name = name },
                DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);

            return ToInfo(response);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<NamedActorReminderInfo>> ListAsync(
        string actorType,
        ActorId? actorId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        var request = new P.ListActorRemindersRequest { ActorType = actorType };
        if (actorId.HasValue)
        {
            request.ActorId = actorId.Value.Value;
        }

        var response = await client.Value.ListActorRemindersAsync(
            request,
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);

        return response.Reminders
            .Select(reminder => new NamedActorReminderInfo(reminder.Name, ToInfo(reminder.Reminder)))
            .ToArray();
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

    /// <inheritdoc />
    public async ValueTask CancelAllAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        var request = new P.UnregisterActorRemindersByTypeRequest { ActorType = actorType };
        if (actorId.HasValue)
        {
            request.ActorId = actorId.Value.Value;
        }

        await client.Value.UnregisterActorRemindersByTypeAsync(
            request,
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    private static Lazy<P.Dapr.DaprClient> CreateEagerAccessor(P.Dapr.DaprClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new Lazy<P.Dapr.DaprClient>(() => client);
    }

    private ActorReminderInfo ToInfo(P.GetActorReminderResponse response) =>
        new(
            response.ActorType,
            ActorId.Create(response.ActorId),
            ActorScheduleDurationParser.ParseOptional(response.HasDueTime ? response.DueTime : null),
            ActorScheduleDurationParser.ParseOptional(response.HasPeriod ? response.Period : null),
            DecodeJson(response.Data),
            ActorScheduleDurationParser.ParseOptional(response.HasTtl ? response.Ttl : null));

    private ActorReminderInfo ToInfo(P.ActorReminder reminder) =>
        new(
            reminder.ActorType,
            ActorId.Create(reminder.ActorId),
            ActorScheduleDurationParser.ParseOptional(reminder.HasDueTime ? reminder.DueTime : null),
            ActorScheduleDurationParser.ParseOptional(reminder.HasPeriod ? reminder.Period : null),
            DecodeJson(reminder.Data),
            ActorScheduleDurationParser.ParseOptional(reminder.HasTtl ? reminder.Ttl : null));

    private static string? DecodeJson(Any? data)
    {
        if (data?.Value is not { Length: > 0 } bytes)
        {
            return null;
        }

        if (TryDecodeBytesValue(bytes, out var nestedBytes))
        {
            return DecodePayload(nestedBytes, unwrapJsonBase64String: true);
        }

        return DecodePayload(bytes, unwrapJsonBase64String: false);
    }

    private static bool TryDecodeBytesValue(ByteString bytes, out ByteString nestedBytes)
    {
        try
        {
            nestedBytes = BytesValue.Parser.ParseFrom(bytes).Value;
            return nestedBytes.Length > 0;
        }
        catch (InvalidProtocolBufferException)
        {
            nestedBytes = ByteString.Empty;
            return false;
        }
    }

    private static string? DecodePayload(ByteString bytes, bool unwrapJsonBase64String)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        var text = System.Text.Encoding.UTF8.GetString(bytes.Span);
        if (!unwrapJsonBase64String)
        {
            return text;
        }

        try
        {
            var encoded = System.Text.Json.JsonSerializer.Deserialize<string>(text);
            if (string.IsNullOrEmpty(encoded))
            {
                return text;
            }

            var decoded = Convert.FromBase64String(encoded);
            return System.Text.Encoding.UTF8.GetString(decoded);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return text;
        }
    }
}
