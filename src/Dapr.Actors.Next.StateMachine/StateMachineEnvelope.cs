namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Persisted state-machine envelope containing the discrete state, extended state, and durable deferred events.
/// </summary>
public sealed record StateMachineEnvelope<TState, TData>(
    TState CurrentState,
    TData Data,
    IReadOnlyList<DeferredEventEnvelope> DeferredEvents)
    where TState : struct, Enum;

/// <summary>
/// Persisted representation of a durable deferred event.
/// </summary>
public sealed record DeferredEventEnvelope(string TypeName, string Json);
