using System.Collections.Concurrent;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// In-memory actor state store used by tests and local hosts without a sidecar adapter.
/// </summary>
public sealed class InMemoryActorStateStore : IActorStateStore
{
    private readonly ConcurrentDictionary<StateKey, byte[]> values = new();

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
    {
        return values.TryGetValue(new StateKey(actorType, actorId, name), out var value)
            ? new ValueTask<ReadOnlyMemory<byte>?>(value)
            : new ValueTask<ReadOnlyMemory<byte>?>((ReadOnlyMemory<byte>?)null);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        values[new StateKey(actorType, actorId, name)] = value.ToArray();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
    {
        values.TryRemove(new StateKey(actorType, actorId, name), out _);
        return ValueTask.CompletedTask;
    }

    private readonly record struct StateKey(string ActorType, string ActorId, string Name);
}
