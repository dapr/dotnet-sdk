namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Configures a state machine over discrete and extended actor state.
/// </summary>
public interface IStateMachine<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Sets the initial state for a new actor instance.
    /// </summary>
    IStateMachine<TState, TData> InitialState(TState state);

    /// <summary>
    /// Configures a state.
    /// </summary>
    IStateConfiguration<TState, TData> In(TState state);

    /// <summary>
    /// Configures the global unhandled event fallback.
    /// </summary>
    IStateMachine<TState, TData> OnUnhandled(Func<IEffectContext<TState, TData, object>, ValueTask> handler);
}
