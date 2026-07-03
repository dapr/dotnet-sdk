namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Base type for actor implementations authored as state machines.
/// </summary>
public abstract class StateMachineActor<TState, TData> : Actor
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the current discrete state.
    /// </summary>
    protected abstract TState CurrentState { get; }

    /// <summary>
    /// Gets the current extended state payload.
    /// </summary>
    protected abstract TData Data { get; }

    /// <summary>
    /// Configures the state machine table.
    /// </summary>
    protected abstract void Configure(IStateMachine<TState, TData> stateMachine);

    /// <summary>
    /// Raises an event into the state machine and returns the reply value.
    /// </summary>
    protected abstract Task<TReply> RaiseAsync<TEvent, TReply>(TEvent evt, CancellationToken cancellationToken = default);
}
