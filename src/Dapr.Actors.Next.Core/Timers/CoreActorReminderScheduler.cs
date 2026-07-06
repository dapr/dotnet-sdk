using System.Collections.Concurrent;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// In-memory reminder scheduler backed by <see cref="TimeProvider"/> and actor reminder turns.
/// </summary>
public sealed class CoreActorReminderScheduler(IActorRuntime runtime, IActorWireSerializer serializer, TimeProvider timeProvider) : IActorReminderScheduler, IDisposable
{
    private readonly ConcurrentDictionary<ReminderKey, ITimer> reminders = [];

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
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

        var key = new ReminderKey(actorType, actorId.Value, name);
        var timer = timeProvider.CreateTimer(
            static state =>
            {
                var callback = (ReminderCallbackState)state!;
                _ = callback.Runtime.DispatchAsync(
                    new ActorRuntimeRequest(
                        callback.ActorType,
                        callback.ActorId,
                        callback.Name,
                        ActorTurnKind.Reminder,
                        callback.Serializer.JsonToBytes(callback.ArgumentsJson),
                        ActorHeaders.Empty,
                        new ActorRequestContext(null, null, ActorHeaders.Empty)),
                    CancellationToken.None);
            },
            new ReminderCallbackState(runtime, serializer, actorType, actorId, name, argumentsJson),
            dueTime,
            period == TimeSpan.Zero ? Timeout.InfiniteTimeSpan : period);

        if (reminders.TryGetValue(key, out var existing))
        {
            if (overwrite == false)
            {
                timer.Dispose();
                throw new InvalidOperationException($"Reminder '{name}' is already scheduled for actor '{actorType}/{actorId.Value}'.");
            }

            existing.Dispose();
        }

        reminders[key] = timer;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        if (reminders.TryRemove(new ReminderKey(actorType, actorId.Value, name), out var timer))
        {
            timer.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var reminder in reminders.Values)
        {
            reminder.Dispose();
        }

        reminders.Clear();
    }

    private sealed record ReminderCallbackState(
        IActorRuntime Runtime,
        IActorWireSerializer Serializer,
        string ActorType,
        ActorId ActorId,
        string Name,
        string ArgumentsJson);

    private readonly record struct ReminderKey(string ActorType, string ActorId, string Name);
}
