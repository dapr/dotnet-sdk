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
            serializer,
            timeProvider,
            actorType,
            actorId,
            operationName,
            argumentsJson,
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
                        callback.Serializer.JsonToBytes(callback.ArgumentsJson),
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
        IActorWireSerializer Serializer,
        TimeProvider TimeProvider,
        string ActorType,
        ActorId ActorId,
        string OperationName,
        string ArgumentsJson,
        IReadOnlyDictionary<string, string> Headers,
        DateTimeOffset? ExpiresAt)
    {
        private ITimer? timer;

        public void Attach(ITimer activeTimer) => timer = activeTimer;

        public void DisposeTimer() => timer?.Dispose();
    }

    private readonly record struct TimerKey(string ActorType, string ActorId, string Name);
}
