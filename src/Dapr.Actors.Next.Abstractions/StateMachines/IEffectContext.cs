namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Provides effects available to a state machine handler.
/// </summary>
public interface IEffectContext<out TState, TData, out TEvent>
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the current state.
    /// </summary>
    TState State { get; }

    /// <summary>
    /// Gets the extended state payload.
    /// </summary>
    TData Data { get; }

    /// <summary>
    /// Gets the event being handled.
    /// </summary>
    TEvent Event { get; }

    /// <summary>
    /// Gets timer effects for the actor.
    /// </summary>
    IActorTimerEffects Timers { get; }

    /// <summary>
    /// Updates the extended state payload.
    /// </summary>
    void Update(Func<TData, TData> update);

    /// <summary>
    /// Raises an internal event in the same turn.
    /// </summary>
    void Raise<TInternalEvent>(TInternalEvent evt);

    /// <summary>
    /// Supplies the method reply.
    /// </summary>
    void Reply<TReply>(TReply value);
}
