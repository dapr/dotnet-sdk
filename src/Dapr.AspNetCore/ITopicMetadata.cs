// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// ITopicMetadata that describes an endpoint as a subscriber to a topic.
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

        /// <summary>
        /// The CEL expression to use to match events for this handler.
        /// </summary>
        string Match { get; }

        /// <summary>
        /// The priority in which this rule should be evaluated (lower to higher).
        /// </summary>
        int Priority { get; }
    }
}
