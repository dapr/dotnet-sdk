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

        var key = new TimerKey(actorType, actorId.Value, name);
        var timer = timeProvider.CreateTimer(
            static state =>
            {
                var callback = (TimerCallbackState)state!;
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
            new TimerCallbackState(runtime, serializer, actorType, actorId, operationName, argumentsJson, headers ?? ActorHeaders.Empty),
            dueTime,
            Timeout.InfiniteTimeSpan);

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
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await CancelAsync(actorType, actorId, name, cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(actorType, actorId, name, dueTime, operationName, argumentsJson, headers, cancellationToken).ConfigureAwait(false);
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
        string ActorType,
        ActorId ActorId,
        string OperationName,
        string ArgumentsJson,
        IReadOnlyDictionary<string, string> Headers);

    private readonly record struct TimerKey(string ActorType, string ActorId, string Name);
}
