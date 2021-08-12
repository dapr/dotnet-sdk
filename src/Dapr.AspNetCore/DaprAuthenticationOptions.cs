// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Authentication;

namespace Dapr.AspNetCore
{
    /// <summary>
    /// Options class provides information needed to control Dapr Authentication handler behavior.
    /// See https://docs.dapr.io/operations/security/api-token/ for more information about API token authentication in Dapr.
    /// </summary>
    public class DaprAuthenticationOptions : AuthenticationSchemeOptions
    {
        internal const string DefaultScheme = "Dapr";
        internal string Scheme { get; } = DefaultScheme;

        /// <summary>
        /// Gets or sets the Dapr API token.
        /// By default, the token will be read from the DAPR_API_TOKEN environment variable.
        /// </summary>
        public string Token { get; set; } = DaprDefaults.GetDefaultApiToken();
    }
}
