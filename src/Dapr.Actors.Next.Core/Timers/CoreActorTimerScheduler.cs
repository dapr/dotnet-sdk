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
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Timer scheduler backed by <see cref="TimeProvider"/> and actor timer turns.
/// </summary>
public sealed class CoreActorTimerScheduler(IActorRuntime runtime, IActorWireSerializer serializer, TimeProvider timeProvider) : IActorTimerScheduler, IDisposable
{
    private readonly ConcurrentDictionary<TimerKey, ITimer> timers = [];

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
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
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            serializer.JsonToBytes(argumentsJson),
            period,
            ttl,
            headers);
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
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
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            arguments.ToArray(),
            period,
            ttl,
            headers);
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync<TArguments>(
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
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            serializer.SerializeToBytes(arguments),
            period,
            ttl,
            headers);
    }

    private ValueTask ScheduleCoreAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        byte[] arguments,
        TimeSpan? period,
        TimeSpan? ttl,
        IReadOnlyDictionary<string, string>? headers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime), "Due time cannot be negative.");
        }

        DaprSidecarActorTimerScheduler.ValidatePeriod(period);
        DaprSidecarActorTimerScheduler.ValidateTtl(dueTime, ttl);

        var key = new TimerKey(actorType, actorId.Value, name);
        var periodValue = period.GetValueOrDefault(Timeout.InfiniteTimeSpan);
        var callbackState = new TimerCallbackState(
            runtime,
            timeProvider,
            actorType,
            actorId,
            operationName,
            arguments,
            headers ?? ActorHeaders.Empty,
            ttl.HasValue ? timeProvider.GetUtcNow() + ttl.Value : null);
        var timer = timeProvider.CreateTimer(
            static state =>
            {
                var callback = (TimerCallbackState)state!;
                if (callback.ExpiresAt.HasValue && callback.TimeProvider.GetUtcNow() > callback.ExpiresAt.Value)
                {
                    callback.DisposeTimer();
                    return;
                }

                _ = callback.Runtime.DispatchAsync(
                    new ActorRuntimeRequest(
                        callback.ActorType,
                        callback.ActorId,
                        callback.OperationName,
                        ActorTurnKind.Timer,
                        callback.Arguments,
                        callback.Headers,
                        new ActorRequestContext(null, null, ActorHeaders.Empty)),
                    CancellationToken.None);
            },
            callbackState,
            dueTime,
            periodValue > TimeSpan.Zero ? periodValue : Timeout.InfiniteTimeSpan);
        callbackState.Attach(timer);

        if (timers.TryGetValue(key, out var existing))
        {
            existing.Dispose();
        }

        timers[key] = timer;
        return ValueTask.CompletedTask;
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
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        if (timers.TryRemove(new TimerKey(actorType, actorId.Value, name), out var timer))
        {
            timer.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var timer in timers.Values)
        {
            timer.Dispose();
        }

        timers.Clear();
    }

    private sealed record TimerCallbackState(
        IActorRuntime Runtime,
        TimeProvider TimeProvider,
        string ActorType,
        ActorId ActorId,
        string OperationName,
        byte[] Arguments,
        IReadOnlyDictionary<string, string> Headers,
        DateTimeOffset? ExpiresAt)
    {
        private ITimer? timer;

        public void Attach(ITimer activeTimer) => timer = activeTimer;

        public void DisposeTimer() => timer?.Dispose();
    }

    private readonly record struct TimerKey(string ActorType, string ActorId, string Name);
}
