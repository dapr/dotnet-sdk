// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using Dapr;
using Dapr.AspNetCore;

namespace Microsoft.AspNetCore.Authentication
{
    /// <summary>
    /// Provides extension methods for <see cref="AuthenticationBuilder" />.
    /// </summary>
    public static class DaprAuthenticationBuilderExtensions
    {
        /// <summary>
        /// Adds Dapr API token authentication.
        /// See https://docs.dapr.io/operations/security/api-token/ for more information about API token authentication in Dapr.
        /// By default, the token will be read from the DAPR_API_TOKEN environment variable.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
        public static AuthenticationBuilder AddDapr(this AuthenticationBuilder builder) => builder.AddDapr(configureOptions: null);

        /// <summary>
        /// Adds Dapr API token authentication.
        /// See https://docs.dapr.io/operations/security/api-token/ for more information about API token authentication in Dapr.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="configureOptions">
        /// A delegate that allows configuring <see cref="DaprAuthenticationOptions"/>.
        /// By default, the token will be read from the DAPR_API_TOKEN environment variable.
        /// </param>
        /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
        public static AuthenticationBuilder AddDapr(this AuthenticationBuilder builder, Action<DaprAuthenticationOptions> configureOptions)
        {
            return builder
                .AddScheme<DaprAuthenticationOptions, DaprAuthenticationHandler>(
                    DaprAuthenticationOptions.DefaultScheme,
                    configureOptions);
        }
    }
}
