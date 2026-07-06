namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Defines the policy that selects the latest actor state version from a set of candidates that share the same canonical name.
/// </summary>
public interface IActorStateVersionSelector
{
    /// <summary>
    /// Selects the latest version identity from a non-empty set of candidates.
    /// </summary>
    ActorStateVersionIdentity SelectLatest(
        string canonicalName,
        IReadOnlyCollection<ActorStateVersionIdentity> candidates,
        IActorStateVersionStrategy strategy);
}
