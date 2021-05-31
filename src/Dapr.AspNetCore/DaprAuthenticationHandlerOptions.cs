// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Authentication;

    internal class DaprAuthenticationHandlerOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "Dapr";
        public string Scheme { get; } = DefaultScheme;

        public Func<string> TokenFactory { get; set; } = DaprDefaults.GetDefaultApiToken;
    }
}
