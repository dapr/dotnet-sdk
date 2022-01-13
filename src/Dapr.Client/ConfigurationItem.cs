using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Class that represents an item fetched from the Dapr Configuration API.
    /// </summary>
    public class ConfigurationItem
    {
        /// <summary>
        /// Constructor for a ConfigurationItem.
        /// </summary>
        /// <param name="key">The key of the configuration item.</param>
        /// <param name="value">The value of the configuration item.</param>
        /// <param name="version">The version of the fetched item.</param>
        /// <param name="metadata">The metadata associated with the request.</param>
        public ConfigurationItem(string key, string value, string version, IReadOnlyDictionary<string, string> metadata)
        {
            Key = key;
            Value = value;
            Version = version;
            Metadata = metadata;
        }

        /// <summary>
        /// The name of the configuration item.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The value of the configuration item.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The version of the item retrieved. This is only provided on responses and
        /// the statestore is not expected to keep all versions available.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The metadata that is passed to/from the statestore component.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }
}
