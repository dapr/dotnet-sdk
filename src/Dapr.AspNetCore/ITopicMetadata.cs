// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// Metadata that describes an endpoint as a subscriber to a topic.
    /// </summary>
    public interface ITopicMetadata
    {
        /// <summary>
        /// Gets the topic name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the pubsub component name name.
        /// </summary>
        string PubsubName { get; }
    }
}
