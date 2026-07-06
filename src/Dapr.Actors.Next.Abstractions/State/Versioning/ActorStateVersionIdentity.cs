namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Identifies a single actor state version within a canonical family.
/// </summary>
/// <param name="CanonicalName">The canonical family name.</param>
/// <param name="Version">The strategy-defined version string.</param>
/// <param name="TypeName">The CLR type name that implements this actor state version.</param>
/// <param name="AssemblyName">Optional assembly name that contains the state type.</param>
public readonly record struct ActorStateVersionIdentity(
    string CanonicalName,
    string Version,
    string TypeName,
    string? AssemblyName = null)
{
    /// <inheritdoc />
    public override string ToString() => $"{CanonicalName}@{Version} ({TypeName})";
}
