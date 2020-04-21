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

            builder.UseMiddleware<CloudEventsMiddleware>();
            return builder;
        }
         

        /// <summary>
        /// Adds the FromForm middleware to the middleware pipeline. The middleware will leverage a controller to convert
        /// Form URL Encoded Posts into JSON.  Primary used to prepare payload against Actors
        /// </summary>
        /// <param name="builder">An <see cref="IApplicationBuilder" />.</param>
        /// <returns>The <see cref="IApplicationBuilder" />.</returns>
        public static IApplicationBuilder UseTwilioWebHooks(this IApplicationBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.UseMiddleware<TwilioWebHookMiddleware>();
            return builder;
        }
    }
}
