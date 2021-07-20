// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Authorization
{
    using Dapr.AspNetCore;

    /// <summary>
    /// Provides extension methods for <see cref="AuthorizationOptions" />.
    /// </summary>
    public static class DaprAuthorizationOptionsExtensions
    {
        /// <summary>
        /// Adds Dapr authorization policy.
        /// </summary>
        /// <param name="options">The <see cref="AuthorizationOptions"/>.</param>
        /// <param name="name">The name of the policy.</param>
        public static void AddDapr(this AuthorizationOptions options, string name = "Dapr")
        {
            options.AddPolicy(name, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddAuthenticationSchemes(DaprAuthenticationOptions.DefaultScheme);
            });
        }
    }
}
