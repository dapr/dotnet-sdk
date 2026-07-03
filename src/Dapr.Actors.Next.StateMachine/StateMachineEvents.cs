namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Event raised when a named state-machine timer fires.
/// </summary>
public sealed record StateMachineTimerFired(string Name);

/// <summary>
/// Event raised when a state's declarative timeout fires while the actor still occupies that state.
/// </summary>
public sealed record StateTimeout<TState>(TState State)
    where TState : struct, Enum;

/// <summary>
/// Acknowledgment value that command methods may return for durable deferral.
/// </summary>
public readonly record struct DeferredEventAck(bool Deferred)
{
    /// <summary>
    /// Gets the accepted/deferred acknowledgment.
    /// </summary>
    public static DeferredEventAck Accepted { get; } = new(true);
}
