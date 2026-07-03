namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Resolves named capabilities for interpreted actor state machines.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Attempts to resolve an effect by name.
    /// </summary>
    bool TryGetEffect(string name, out IActorEffect effect);

    /// <summary>
    /// Attempts to resolve a guard by name.
    /// </summary>
    bool TryGetGuard(string name, out IActorGuard guard);
}
