using System.Collections.Concurrent;
using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// In-memory interpreted machine-definition store for tests and single-process hosts.
/// </summary>
public sealed class InMemoryInterpretedMachineStore : IInterpretedMachineStore
{
    private readonly ConcurrentDictionary<Key, InterpretedMachineDefinition> definitions = [];

    /// <inheritdoc />
    public ValueTask<InterpretedMachineDefinition?> GetAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        definitions.TryGetValue(new Key(actorType, actorId.Value), out var definition);
        return ValueTask.FromResult(definition);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string actorType, ActorId actorId, InterpretedMachineDefinition definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        definitions[new Key(actorType, actorId.Value)] = definition;
        return ValueTask.CompletedTask;
    }

    private sealed record Key(string ActorType, string ActorId);
}
