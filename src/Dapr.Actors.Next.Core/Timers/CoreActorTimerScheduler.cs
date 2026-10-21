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
/// Timers are removed when their owning activation ends, including failed activation.
/// Reminders have a separate lifetime and are not affected.
/// </remarks>
public sealed class CoreActorTimerScheduler : IActorTimerScheduler, IDisposable
{
    private readonly IActorRuntime runtime;
    private readonly IActorWireSerializer serializer;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CoreActorTimerScheduler>? logger;
    private readonly object sync = new();
    private readonly Dictionary<TimerKey, TimerRegistration> timers = [];
    private bool disposed;

    /// <summary>
    /// Initializes an activation-scoped timer scheduler with callback failure reporting.
    /// </summary>
    public CoreActorTimerScheduler(IActorRuntime runtime, IActorWireSerializer serializer,
        TimeProvider timeProvider, ILogger<CoreActorTimerScheduler>? logger)
    {
        this.runtime = runtime;
        this.serializer = serializer;
        this.timeProvider = timeProvider;
        this.logger = logger;
        if (runtime is ActorRuntime actorRuntime)
            actorRuntime.ActivationEnded += CancelActivationTimers;
    }

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
        var info = new ActorTimerInfo(actorType, actorId, dueTime, period, System.Text.Encoding.UTF8.GetString(arguments), ttl, operationName);
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
            var expiresAt = ttl.HasValue ? timeProvider.GetUtcNow() + ttl.Value : (DateTimeOffset?)null;
            var timer = new ScheduledActorCallback(sync, runtime, timeProvider, request, expiresAt, logger);
            timers.TryGetValue(key, out var existing);
            timers[key] = new TimerRegistration(timer, info, expiresAt);
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

            existing?.Timer.Stop();
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
    public ValueTask<ActorTimerInfo?> GetAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            return ValueTask.FromResult(
                timers.TryGetValue(new TimerKey(actorType, actorId.Value, name), out var registration) && !Expired(registration)
                    ? registration.Info
                    : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<NamedActorTimerInfo>> ListAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposed, this);
            var result = timers
                .Where(pair =>
                    string.Equals(pair.Key.ActorType, actorType, StringComparison.Ordinal)
                    && string.Equals(pair.Key.ActorId, actorId.Value, StringComparison.Ordinal)
                    && !Expired(pair.Value))
                .Select(static pair => new NamedActorTimerInfo(pair.Key.Name, pair.Value.Info))
                .ToArray();

            return ValueTask.FromResult<IReadOnlyList<NamedActorTimerInfo>>(result);
        }
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
            if (timers.Remove(new TimerKey(actorType, actorId.Value, name), out var registration))
            {
                registration.Timer.Stop();
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
            if (runtime is ActorRuntime actorRuntime)
                actorRuntime.ActivationEnded -= CancelActivationTimers;
            foreach (var registration in timers.Values)
            {
                registration.Timer.Stop();
            }

            timers.Clear();
        }
    }

    private void CancelActivationTimers(string actorType, ActorId actorId)
    {
        lock (sync)
        {
            foreach (var key in timers.Keys.Where(key => key.ActorType == actorType && key.ActorId == actorId.Value).ToArray())
            {
                timers[key].Timer.Stop();
                timers.Remove(key);
            }
        }
    }

    private readonly record struct TimerKey(string ActorType, string ActorId, string Name);

    // Same rule as dispatch: a timer may still fire exactly at its deadline, so it is expired only after it.
    private bool Expired(TimerRegistration registration) =>
        registration.ExpiresAt.HasValue && timeProvider.GetUtcNow() > registration.ExpiresAt.Value;

    private sealed record TimerRegistration(ScheduledActorCallback Timer, ActorTimerInfo Info, DateTimeOffset? ExpiresAt);
}
