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
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TopicAttribute : Attribute
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
        /// Gets the topic name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the pubsub component name name.
        /// </summary>
        public string PubsubName { get; }
    }
}
