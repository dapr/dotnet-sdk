namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Default no-op state fault injector used outside deterministic tests.
/// </summary>
public sealed class NoopActorStateFaultInjector : IActorStateFaultInjector
{
    /// <inheritdoc />
    public ValueTask BeforeWriteAsync(
        Type stateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask BeforeMigrationAsync(
        Type targetStateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask BeforeUpcastHopAsync(
        Type fromStateType,
        Type toStateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
