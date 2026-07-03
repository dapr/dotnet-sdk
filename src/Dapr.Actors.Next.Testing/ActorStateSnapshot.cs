namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Provides typed access to an actor's persisted state during a deterministic test.
/// </summary>
public sealed class ActorStateSnapshot(ActorTestRuntime runtime, string actorType, string actorId)
{
    /// <summary>
    /// Reads a named state value.
    /// </summary>
    public T? Get<T>(string name = "state") => runtime.ReadState<T>(actorType, actorId, name);

    /// <summary>
    /// Reads the default current-state slot used by state-machine tests.
    /// </summary>
    public TState? CurrentState<TState>() => runtime.ReadState<TState>(actorType, actorId, "__currentState");

    /// <summary>
    /// Reads the default data slot used by state-machine tests.
    /// </summary>
    public TData? Data<TData>() => runtime.ReadState<TData>(actorType, actorId, "__data");
}
