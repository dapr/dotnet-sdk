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
    /// Cancels a durable actor reminder.
    /// </summary>
    ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default);
}
