namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Configures one event handler branch.
/// </summary>
public interface IEventBranchConfiguration<TState, TData, out TEvent>
    where TState : struct, Enum
{
    /// <summary>
    /// Runs an effect and remains in the current state unless a transition is also configured.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Do(Func<IEffectContext<TState, TData, TEvent>, ValueTask> effect);

    /// <summary>
    /// Transitions to another state.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> GoTo(TState state);

    /// <summary>
    /// Replies to the invoking actor method.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Reply<TReply>(TReply value);
}
