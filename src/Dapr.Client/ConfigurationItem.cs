using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Class that represents an item fetched from the Dapr Configuration API.
/// </summary>
/// <param name="value">The value of the configuration item.</param>
/// <param name="version">The version of the fetched item.</param>
/// <param name="metadata">The metadata associated with the request.</param>
public class ConfigurationItem(string value, string version, IReadOnlyDictionary<string, string> metadata)
{
    /// <summary>
    /// The value of the configuration item.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// The version of the item retrieved. This is only provided on responses and
    /// the statestore is not expected to keep all versions available.
    /// </summary>
    public string Version { get; } = version;

    /// <summary>
    /// The metadata that is passed to/from the statestore component.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; } = metadata;
}
