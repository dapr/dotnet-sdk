// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;

    /// <summary>
    /// Metadata that describes an endpoint as a subscriber to a topic.
    /// </summary>
    public class TopicAttribute : Attribute, ITopicMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        public TopicAttribute(string pubsubName, string name)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

            this.Name = name;
            this.PubsubName = pubsubName;
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string PubsubName { get; }
    }
}
