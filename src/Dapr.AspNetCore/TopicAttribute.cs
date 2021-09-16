// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;

    /// <summary>
    /// TopicAttribute describes an endpoint as a subscriber to a topic.
    /// </summary>
    public class TopicAttribute : Attribute, ITopicMetadata, IRawTopicMetadata
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

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
        public TopicAttribute(string pubsubName, string name, bool enableRawPayload)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

            this.Name = name;
            this.PubsubName = pubsubName;
            this.EnableRawPayload = enableRawPayload;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <param name="match">The CEL expression to test the cloud event with.</param>
        /// <param name="priority">The priority of the rule (low-to-high values).</param>
        public TopicAttribute(string pubsubName, string name, string match, int priority)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

            this.Name = name;
            this.PubsubName = pubsubName;
            this.Match = match;
            this.Priority = priority;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
        /// <param name="match">The CEL expression to test the cloud event with.</param>
        /// <param name="priority">The priority of the rule (low-to-high values).</param>
        public TopicAttribute(string pubsubName, string name, bool enableRawPayload, string match, int priority)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

            this.Name = name;
            this.PubsubName = pubsubName;
            this.EnableRawPayload = enableRawPayload;
            this.Match = match;
            this.Priority = priority;
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string PubsubName { get; }

        /// <inheritdoc/>
        public bool? EnableRawPayload { get; }

        /// <inheritdoc/>
        public new string Match { get; }

        /// <inheritdoc/>
        public int Priority { get; }
    }
}
