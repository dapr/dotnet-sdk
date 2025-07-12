// ------------------------------------------------------------------------
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

using System;
using Dapr.AspNetCore;

namespace Microsoft.AspNetCore.Authentication;

/// <summary>
/// Provides extension methods for <see cref="AuthenticationBuilder" />.
/// </summary>
public static class DaprAuthenticationBuilderExtensions
{
    /// <summary>
    /// Adds App API token authentication.
    /// See https://docs.dapr.io/operations/security/app-api-token/ for more information about App API token authentication in Dapr.
    /// By default, the token will be read from the APP_API_TOKEN environment variable.
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the operation has completed.</returns>
    public static AuthenticationBuilder AddDapr(this AuthenticationBuilder builder) => builder.AddDapr(configureOptions: null);

    /// <summary>
    /// Adds App API token authentication.
    /// See https://docs.dapr.io/operations/security/app-api-token/ for more information about App API token authentication in Dapr.
    /// </summary>
    /// <param name="builder">The <see cref="AuthenticationBuilder"/>.</param>
    /// <param name="configureOptions">
    /// A delegate that allows configuring <see cref="DaprAuthenticationOptions"/>.
    /// By default, the token will be read from the APP_API_TOKEN environment variable.
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