using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Schedules durable actor reminder turns through the runtime.
/// </summary>
public interface IActorReminderScheduler
{
    /// <summary>
    /// Registers or updates a durable actor reminder.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        string argumentsJson,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates a durable actor reminder with a caller-provided wire payload.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        byte[] arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates a durable actor reminder with a typed payload serialized by the configured actor wire serializer.
    /// </summary>
    ValueTask ScheduleAsync<TArguments>(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        TimeSpan period,
        TArguments arguments,
        TimeSpan? ttl = null,
        bool? overwrite = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a durable actor reminder registration by name.
    /// </summary>
    ValueTask<ActorReminderInfo?> GetAsync(
        string actorType,
        ActorId actorId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists durable actor reminders for an actor type, optionally scoped to one actor id.
    /// </summary>
    ValueTask<IReadOnlyList<NamedActorReminderInfo>> ListAsync(
        string actorType,
        ActorId? actorId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a durable actor reminder.
    /// </summary>
    ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels all durable actor reminders for an actor type, optionally scoped to one actor id.
    /// </summary>
    ValueTask CancelAllAsync(string actorType, ActorId? actorId = null, CancellationToken cancellationToken = default);
}
