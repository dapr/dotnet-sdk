using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Stores verified interpreted machine definitions.
/// </summary>
public interface IInterpretedMachineStore
{
    /// <summary>
    /// Gets the definition for an interpreted actor instance.
    /// </summary>
    ValueTask<InterpretedMachineDefinition?> GetAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a verified definition for an interpreted actor instance.
    /// </summary>
    ValueTask SetAsync(string actorType, ActorId actorId, InterpretedMachineDefinition definition, CancellationToken cancellationToken = default);
}
