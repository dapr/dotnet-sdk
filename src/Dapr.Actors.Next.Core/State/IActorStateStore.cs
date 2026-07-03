namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Persists actor state envelopes for Core.
/// </summary>
public interface IActorStateStore
{
    /// <summary>
    /// Reads a state value.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a state value.
    /// </summary>
    ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a state value.
    /// </summary>
    ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default);
}
