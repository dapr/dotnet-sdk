using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Schedules actor timer turns through the runtime.
/// </summary>
public interface IActorTimerScheduler
{
    /// <summary>
    /// Schedules a named actor timer.
    /// </summary>
    ValueTask ScheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules a named actor timer.
    /// </summary>
    ValueTask RescheduleAsync(
        string actorType,
        ActorId actorId,
        string name,
        TimeSpan dueTime,
        string operationName,
        string argumentsJson,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a named actor timer.
    /// </summary>
    ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default);
}
