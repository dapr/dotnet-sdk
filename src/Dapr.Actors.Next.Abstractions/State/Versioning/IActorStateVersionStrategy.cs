namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Defines how actor state type names are parsed into canonical names and versions, and how
/// two version strings are compared for ordering.
/// </summary>
/// <remarks>
/// This compile-time-only input is consumed by generators and analyzers; the runtime consumes resolved
/// migration metadata instead.
/// </remarks>
public interface IActorStateVersionStrategy : IComparer<string>
{
    /// <summary>
    /// Attempts to derive a canonical family name and version from an actor state type name.
    /// </summary>
    bool TryParse(string typeName, out string canonicalName, out string version);

    /// <summary>
    /// Compares two version strings and returns their relative order.
    /// </summary>
    new int Compare(string v1, string v2);
}
