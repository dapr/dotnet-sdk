using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Effect-context timer surface backed by the Core actor timer scheduler.
/// </summary>
internal sealed class ActorTimerEffects<TState>(
    IActorTimerScheduler scheduler,
    string actorType,
    ActorId actorId,
    Func<TState> currentState) : IActorTimerEffects
    where TState : struct, Enum
{
    /// <inheritdoc />
    public void Schedule(string name, TimeSpan dueTime) => ScheduleCore(name, dueTime, reschedule: false);

    /// <inheritdoc />
    public void Reschedule(string name, TimeSpan dueTime) => ScheduleCore(name, dueTime, reschedule: true);

    /// <inheritdoc />
    public void Cancel(string name)
    {
        scheduler.CancelAsync(actorType, actorId, name).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Schedules or reschedules a named timer with the serialized state-machine timer payload.
    /// </summary>
    private void ScheduleCore(string name, TimeSpan dueTime, bool reschedule)
    {
        var state = string.Equals(name, StateMachineConstants.StateTimeoutTimerName, StringComparison.Ordinal)
            ? currentState().ToString()
            : null;
        var payload = JsonSerializer.Serialize(new StateMachineTimerPayload(name, state));
        if (reschedule)
        {
            scheduler.RescheduleAsync(actorType, actorId, name, dueTime, StateMachineConstants.TimerOperationName, payload).AsTask().GetAwaiter().GetResult();
        }
        else
        {
            scheduler.ScheduleAsync(actorType, actorId, name, dueTime, StateMachineConstants.TimerOperationName, payload).AsTask().GetAwaiter().GetResult();
        }
    }
}

/// <summary>
/// Serialized payload delivered by the reserved state-machine timer operation.
/// </summary>
internal sealed record StateMachineTimerPayload
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineTimerPayload"/> record.
    /// </summary>
    public StateMachineTimerPayload(string name, string? state)
    {
        Name = name;
        State = state;
    }

    /// <summary>
    /// Gets the timer name that fired.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the state name expected for a declarative state timeout.
    /// </summary>
    public string? State { get; init; }
}
