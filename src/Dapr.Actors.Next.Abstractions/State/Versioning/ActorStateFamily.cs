namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Represents all discovered versions of an actor state family that share the same canonical name.
/// </summary>
/// <param name="CanonicalName">The canonical name shared by the versions in this family.</param>
/// <param name="Versions">The unordered collection of versions discovered for this family.</param>
public sealed record ActorStateFamily(string CanonicalName, IReadOnlyCollection<ActorStateVersionIdentity> Versions);
