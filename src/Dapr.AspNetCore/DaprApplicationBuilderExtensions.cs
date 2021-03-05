// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System;
    using Dapr;

    /// <summary>
    /// Provides extension methods for <see cref="IApplicationBuilder" />.
    /// </summary>
    public static class DaprApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the cloud events middleware to the middleware pipeline. The cloud events middleware will unwrap
        /// requests that use the cloud events structured format, allowing the event payload to be read directly.
        /// </summary>
        /// <param name="builder">An <see cref="IApplicationBuilder" />.</param>
        /// <returns>The <see cref="IApplicationBuilder" />.</returns>
        public static IApplicationBuilder UseCloudEvents(this IApplicationBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return UseCloudEvents(builder, new CloudEventsMiddlewareOptions());
        }

        /// <summary>
        /// Adds the cloud events middleware to the middleware pipeline. The cloud events middleware will unwrap
        /// requests that use the cloud events structured format, allowing the event payload to be read directly.
        /// </summary>
        /// <param name="builder">An <see cref="IApplicationBuilder" />.</param>
        /// <param name="options">The <see cref="CloudEventsMiddlewareOptions" /> to configure optional settings.</param>
        /// <returns>The <see cref="IApplicationBuilder" />.</returns>
        public static IApplicationBuilder UseCloudEvents(this IApplicationBuilder builder, CloudEventsMiddlewareOptions options)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.UseMiddleware<CloudEventsMiddleware>(options);
            return builder;
        }
    }
}
