namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Provides an extension point for tests to inject state-store failures at typed state boundaries.
/// </summary>
public interface IActorStateFaultInjector
{
    /// <summary>
    /// Runs before a typed state value is written to the backing store.
    /// </summary>
    ValueTask BeforeWriteAsync(
        Type stateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default);
}
