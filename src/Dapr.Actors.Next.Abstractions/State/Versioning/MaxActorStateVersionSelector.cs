namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Default selector that chooses the maximum version according to the active strategy.
/// </summary>
public sealed class MaxActorStateVersionSelector : IActorStateVersionSelector
{
    /// <inheritdoc />
    public ActorStateVersionIdentity SelectLatest(
        string canonicalName,
        IReadOnlyCollection<ActorStateVersionIdentity> candidates,
        IActorStateVersionStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentOutOfRangeException.ThrowIfEqual(0, candidates.Count, nameof(candidates));

        return candidates.OrderBy(candidate => candidate.Version, strategy).Last();
    }
}
