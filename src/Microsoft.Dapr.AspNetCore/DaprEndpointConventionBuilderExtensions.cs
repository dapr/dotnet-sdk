// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System;
    using Microsoft.Dapr;

    /// <summary>
    /// Contains extension methods for <see cref="IEndpointConventionBuilder" />.
    /// </summary>
    public static class DaprEndpointConventionBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="TopicAttribute" /> metadata to the provided <see cref="IEndpointConventionBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointConventionBuilder" />.</param>
        /// <param name="name">The topic name.</param>
        /// <typeparam name="T">The <see cref="IEndpointConventionBuilder" /> type.</typeparam>
        /// <returns>The <see cref="IEndpointConventionBuilder" /> builder object.</returns>
        public static T WithTopic<T>(this T builder, string name)
            where T : IEndpointConventionBuilder
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(name));
            }

            builder.WithMetadata(new TopicAttribute(name));
            return builder;
        }
    }
}