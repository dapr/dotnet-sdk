using System.Collections.ObjectModel;

namespace Dapr.Actors.Next.Core;

/// <summary>
/// Shared actor header collections for allocation-sensitive runtime paths.
/// </summary>
public static class ActorHeaders
{
    /// <summary>
    /// Gets an immutable empty header dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Empty { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
}
