namespace Dapr.Actors.Next.Abstractions.Registry;

/// <summary>
/// Provides generated actor type and method metadata.
/// </summary>
public interface IActorRegistry
{
    /// <summary>
    /// Gets all generated actor type descriptors.
    /// </summary>
    IReadOnlyList<ActorTypeDescriptor> Actors { get; }

    /// <summary>
    /// Attempts to find a descriptor by actor type name.
    /// </summary>
    bool TryGet(string actorType, out ActorTypeDescriptor descriptor);
}
