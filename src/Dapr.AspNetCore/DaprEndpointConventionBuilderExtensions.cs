﻿// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System;
    using System.Collections.Generic;
    using Dapr;

    /// <summary>
    /// Contains extension methods for <see cref="IEndpointConventionBuilder" />.
    /// </summary>
    public static class DaprEndpointConventionBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="ITopicMetadata" /> metadata to the provided <see cref="IEndpointConventionBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointConventionBuilder" />.</param>\
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <typeparam name="T">The <see cref="IEndpointConventionBuilder" /> type.</typeparam>
        /// <returns>The <see cref="IEndpointConventionBuilder" /> builder object.</returns>
        public static T WithTopic<T>(this T builder, string pubsubName, string name)
            where T : IEndpointConventionBuilder
        {
            return WithTopic(builder, pubsubName, name, false);
        }

        /// <summary>
        /// Adds <see cref="ITopicMetadata" /> metadata to the provided <see cref="IEndpointConventionBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointConventionBuilder" />.</param>\
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <param name="metadata">
        /// A collection of metadata key-value pairs that will be provided to the pubsub. The valid metadata keys and values 
        /// are determined by the type of pubsub component used.
        /// </param>
        /// <typeparam name="T">The <see cref="IEndpointConventionBuilder" /> type.</typeparam>
        /// <returns>The <see cref="IEndpointConventionBuilder" /> builder object.</returns>
        public static T WithTopic<T>(this T builder, string pubsubName, string name, IDictionary<string, string> metadata)
            where T : IEndpointConventionBuilder
        {
            return WithTopic(builder, pubsubName, name, false, metadata);
        }

        /// <summary>
        /// Adds <see cref="ITopicMetadata" /> metadata to the provided <see cref="IEndpointConventionBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointConventionBuilder" />.</param>\
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
        /// <typeparam name="T">The <see cref="IEndpointConventionBuilder" /> type.</typeparam>
        /// <returns>The <see cref="IEndpointConventionBuilder" /> builder object.</returns>
        public static T WithTopic<T>(this T builder, string pubsubName, string name, bool enableRawPayload)
            where T : IEndpointConventionBuilder
        {
            return WithTopic(builder, pubsubName, name, enableRawPayload, null);
        }

        /// <summary>
        /// Adds <see cref="ITopicMetadata" /> metadata to the provided <see cref="IEndpointConventionBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointConventionBuilder" />.</param>\
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="name">The topic name.</param>
        /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
        /// <param name="metadata">
        /// A collection of metadata key-value pairs that will be provided to the pubsub. The valid metadata keys and values 
        /// are determined by the type of pubsub component used.
        /// </param>
        /// <typeparam name="T">The <see cref="IEndpointConventionBuilder" /> type.</typeparam>
        /// <returns>The <see cref="IEndpointConventionBuilder" /> builder object.</returns>
        public static T WithTopic<T>(this T builder, string pubsubName, string name, bool enableRawPayload, IDictionary<string, string> metadata)
            where T : IEndpointConventionBuilder
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

            builder.WithMetadata(new TopicAttribute(pubsubName, name, enableRawPayload));
            if (metadata is not null)
            {
                foreach (var md in metadata)
                {
                    builder.WithMetadata(new TopicMetadataAttribute(md.Key, md.Value));
                }
            }
            return builder;
        }
    }
}
