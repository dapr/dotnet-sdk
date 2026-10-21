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
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Transport;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            serializer.JsonToBytes(argumentsJson),
            period,
            ttl,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        await ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            arguments,
            period,
            ttl,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        TArguments arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            serializer.SerializeToBytes(arguments),
            period,
            ttl,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ScheduleCoreAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period,
        TimeSpan? ttl,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        ValidatePeriod(period);
        ValidateTtl(dueTime, ttl);

        var request = new P.RegisterActorTimerRequest
        {
            ActorType = actorType,
            ActorId = actorId.Value,
            Name = name,
            DueTime = ActorScheduleDurationFormatter.Format(dueTime),
            Callback = operationName,
            Data = ByteString.CopyFrom(arguments),
        };

        if (ShouldWritePeriod(period))
        {
            request.Period = ActorScheduleDurationFormatter.Format(period!.Value);
        }

        if (ttl.HasValue)
        {
            request.Ttl = ActorScheduleDurationFormatter.Format(ttl.Value);
        }

        await client.Value.RegisterActorTimerAsync(
            request,
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
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, argumentsJson, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, arguments, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RescheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        TArguments arguments,
        TimeSpan? period = null,
        TimeSpan? ttl = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, arguments, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await client.Value.UnregisterActorTimerAsync(
            new P.UnregisterActorTimerRequest { ActorType = actorType, ActorId = actorId.Value, Name = name },
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<ActorTimerInfo?> GetAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            var response = await client.Value.GetActorTimerAsync(
                new P.GetActorTimerRequest { ActorType = actorType, ActorId = actorId.Value, Name = name },
                DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);

            return ToInfo(response);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<NamedActorTimerInfo>> ListAsync(
        string actorType,
        ActorId actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        var response = await client.Value.ListActorTimersAsync(
            new P.ListActorTimersRequest { ActorType = actorType, ActorId = actorId.Value },
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);

        return response.Timers
            .Select(timer => new NamedActorTimerInfo(timer.Name, ToInfo(timer.Timer)))
            .ToArray();
    }

    private static ActorTimerInfo ToInfo(P.GetActorTimerResponse response) =>
        ToInfo(
            response.ActorType,
            response.ActorId,
            response.HasDueTime ? response.DueTime : null,
            response.HasPeriod ? response.Period : null,
            response.Data,
            response.HasTtl ? response.Ttl : null,
            response.HasCallback ? response.Callback : null);

    private static ActorTimerInfo ToInfo(P.ActorTimer timer) =>
        ToInfo(
            timer.ActorType,
            timer.ActorId,
            timer.HasDueTime ? timer.DueTime : null,
            timer.HasPeriod ? timer.Period : null,
            timer.Data,
            timer.HasTtl ? timer.Ttl : null,
            timer.HasCallback ? timer.Callback : null);

    // The sidecar echoes "dueTime" as registered (a duration or an RFC 3339 timestamp), reports "period" as
    // "@every <Go duration>", and reports "ttl" as the absolute RFC 3339 expiration computed at registration.
    private static ActorTimerInfo ToInfo(
        string actorType, string actorId, string? dueTime, string? period, Any? data, string? ttl, string? callback)
    {
        var (dueDuration, dueAt) = ActorScheduleDurationParser.ParseOptionalDueTime(dueTime);
        return new ActorTimerInfo(
            actorType,
            ActorId.Create(actorId),
            dueDuration,
            dueAt,
            ActorScheduleDurationParser.ParseOptional(period),
            ActorSchedulePayloadDecoder.DecodeJson(data),
            ActorScheduleDurationParser.ParseOptionalTimestamp(ttl),
            callback);
    }

    internal static void ValidatePeriod(TimeSpan? period)
    {
        if (period.HasValue && period.Value < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period cannot be less than Timeout.InfiniteTimeSpan.");
        }
    }

    internal static void ValidateTtl(TimeSpan dueTime, TimeSpan? ttl)
    {
        if (ttl.HasValue && (ttl.Value < TimeSpan.Zero || ttl.Value < dueTime))
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL cannot be negative or earlier than the due time.");
        }
    }

    internal static bool ShouldWritePeriod(TimeSpan? period) =>
        period.HasValue && period.Value >= TimeSpan.Zero;

}
