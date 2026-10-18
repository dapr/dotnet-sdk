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
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Timer scheduler backed by <see cref="TimeProvider"/> and actor timer turns.
/// </summary>
/// <remarks>
/// Registration, replacement, cancellation, and disposal are serialized. Cancellation or replacement
/// prevents new callback admissions; an already admitted actor turn may finish. Callbacks start
/// independent call chains, without the registering actor's execution context or reentrancy headers.
/// A disposed scheduler rejects further operations.
/// </remarks>
public sealed class CoreActorTimerScheduler(
    IActorRuntime runtime,
    IActorWireSerializer serializer,
    TimeProvider timeProvider,
    ILogger<CoreActorTimerScheduler>? logger) : IActorTimerScheduler, IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<TimerKey, ScheduledActorCallback> timers = [];
    private bool disposed;

    /// <summary>
    /// Initializes a timer scheduler with trace-based callback failure reporting.
    /// </summary>
    public CoreActorTimerScheduler(IActorRuntime runtime, IActorWireSerializer serializer, TimeProvider timeProvider)
        : this(runtime, serializer, timeProvider, null)
    {
    }

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
        cancellationToken.ThrowIfCancellationRequested();
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            serializer.JsonToBytes(argumentsJson),
            period,
            ttl,
            headers,
            cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
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
            headers,
            cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            operationName,
            serializer.SerializeToBytes(arguments),
            period,
            ttl,
            headers,
            cancellationToken);
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
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
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
        var request = new ActorRuntimeRequest(
            actorType,
            actorId,
            operationName,
            ActorTurnKind.Timer,
            arguments,
            ScheduledActorCallback.IndependentHeaders(headers),
            new ActorRequestContext(null, null, ActorHeaders.Empty));
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            var timer = new ScheduledActorCallback(sync, runtime, timeProvider, request,
                ttl.HasValue ? timeProvider.GetUtcNow() + ttl.Value : null, logger);
            timers.TryGetValue(key, out var existing);
            timers[key] = timer;
            try
            {
                timer.Start(dueTime, periodValue > TimeSpan.Zero ? periodValue : Timeout.InfiniteTimeSpan);
            }
            catch
            {
                timer.Stop();
                if (existing is null)
                {
                    timers.Remove(key);
                }
                else
                {
                    timers[key] = existing;
                }

                throw;
            }

            existing?.Stop();
        }

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
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, arguments, period, ttl, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (timers.Remove(new TimerKey(actorType, actorId.Value, name), out var timer))
            {
                timer.Stop();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var timer in timers.Values)
            {
                timer.Stop();
            }

            timers.Clear();
        }
    }

    private readonly record struct TimerKey(string ActorType, string ActorId, string Name);
}
