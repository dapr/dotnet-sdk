namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Configures handling for one event type.
/// </summary>
public interface IEventConfiguration<TState, TData, out TEvent>
    where TState : struct, Enum
{
    /// <summary>
    /// Adds a guarded branch.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> When(Func<TData, TEvent, bool> predicate);

    /// <summary>
    /// Adds the fallthrough branch.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Otherwise();
}
