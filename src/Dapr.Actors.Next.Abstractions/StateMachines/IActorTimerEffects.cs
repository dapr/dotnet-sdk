namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Describes timer effects available inside state machine effects.
/// </summary>
public interface IActorTimerEffects
{
    /// <summary>
    /// Schedules a timer.
    /// </summary>
    void Schedule(string name, TimeSpan dueTime);

    /// <summary>
    /// Reschedules a timer.
    /// </summary>
    void Reschedule(string name, TimeSpan dueTime);

    /// <summary>
    /// Cancels a timer.
    /// </summary>
    void Cancel(string name);
}
