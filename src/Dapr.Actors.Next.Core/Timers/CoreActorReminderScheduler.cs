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
    private readonly ConcurrentDictionary<ReminderKey, ReminderRegistration> reminders = [];

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
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            serializer.JsonToBytes(argumentsJson),
            argumentsJson,
            ttl,
            overwrite);
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(
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
        var payload = arguments.ToArray();
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            payload,
            serializer.BytesToJson(payload),
            ttl,
            overwrite);
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync<TArguments>(
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
        var payload = serializer.SerializeToBytes(arguments);
        return ScheduleCoreAsync(
            actorType,
            actorId,
            name,
            dueTime,
            period,
            payload,
            serializer.BytesToJson(payload),
            ttl,
            overwrite);
    }

    private ValueTask ScheduleCoreAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        string? argumentsJson,
        TimeSpan? ttl,
        bool? overwrite)
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
        var info = new ActorReminderInfo(actorType, actorId, dueTime, period, argumentsJson, ttl);
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
                        callback.Arguments,
                        ActorHeaders.Empty,
                        new ActorRequestContext(null, null, ActorHeaders.Empty)),
                    CancellationToken.None);
            },
            new ReminderCallbackState(runtime, actorType, actorId, name, arguments),
            dueTime,
            period == TimeSpan.Zero ? Timeout.InfiniteTimeSpan : period);

        if (reminders.TryGetValue(key, out var existing))
        {
            if (overwrite == false)
            {
                timer.Dispose();
                throw new InvalidOperationException($"Reminder '{name}' is already scheduled for actor '{actorType}/{actorId.Value}'.");
            }

            existing.Timer.Dispose();
        }

        reminders[key] = new ReminderRegistration(timer, info);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ActorReminderInfo?> GetAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return ValueTask.FromResult(
            reminders.TryGetValue(new ReminderKey(actorType, actorId.Value, name), out var registration)
                ? registration.Info
                : null);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<NamedActorReminderInfo>> ListAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        var result = reminders
            .Where(pair =>
                string.Equals(pair.Key.ActorType, actorType, StringComparison.Ordinal)
                && (!actorId.HasValue || string.Equals(pair.Key.ActorId, actorId.Value.Value, StringComparison.Ordinal)))
            .Select(static pair => new NamedActorReminderInfo(pair.Key.Name, pair.Value.Info))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<NamedActorReminderInfo>>(result);
    }

    /// <inheritdoc />
    public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default)
    {
        if (reminders.TryRemove(new ReminderKey(actorType, actorId.Value, name), out var registration))
        {
            registration.Timer.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CancelAllAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        foreach (var pair in reminders.ToArray())
        {
            if (string.Equals(pair.Key.ActorType, actorType, StringComparison.Ordinal)
                && (!actorId.HasValue || string.Equals(pair.Key.ActorId, actorId.Value.Value, StringComparison.Ordinal))
                && reminders.TryRemove(pair.Key, out var registration))
            {
                registration.Timer.Dispose();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var reminder in reminders.Values)
        {
            reminder.Timer.Dispose();
        }

        reminders.Clear();
    }

    private sealed record ReminderCallbackState(
        IActorRuntime Runtime,
        string ActorType,
        ActorId ActorId,
        string Name,
        byte[] Arguments);

    private sealed record ReminderRegistration(ITimer Timer, ActorReminderInfo Info);

    private readonly record struct ReminderKey(string ActorType, string ActorId, string Name);
}
