// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Authentication
{
    using System;
    using Dapr;
    using Dapr.AspNetCore;

    /// <summary>
    /// Provides extension methods for <see cref="AuthenticationBuilder" />.
    /// </summary>
    public static class DaprAuthenticationBuilderExtensions
    {
        /// <summary>
        /// Adds Dapr API token authentication.
        /// See https://docs.dapr.io/operations/security/api-token/ for more information about API token authentication in Dapr.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
        /// <param name="tokenFactory">
        /// A delegate to configure the Dapr API token.
        /// By default, the token will be read from the DAPR_API_TOKEN environment variable.
        /// </param>
        /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
        public static AuthenticationBuilder AddDapr(this AuthenticationBuilder builder, Func<string> tokenFactory = null)
        {
            tokenFactory ??= DaprDefaults.GetDefaultApiToken;

            return builder
                .AddScheme<DaprAuthenticationHandlerOptions, DaprAuthenticationHandler>(
                    DaprAuthenticationHandlerOptions.DefaultScheme,
                    options => options.TokenFactory = tokenFactory);
        }
    }
}
