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

using Microsoft.AspNetCore.Authentication;

namespace Dapr.AspNetCore;

/// <summary>
/// Options class provides information needed to control Dapr Authentication handler behavior.
/// See https://docs.dapr.io/operations/security/app-api-token/ for more information about App API token authentication in Dapr.
/// </summary>
public class DaprAuthenticationOptions : AuthenticationSchemeOptions
{
    internal const string DefaultScheme = "Dapr";
    internal string Scheme { get; } = DefaultScheme;

    /// <summary>
    /// Gets or sets the App API token.
    /// By default, the token will be read from the APP_API_TOKEN environment variable.
    /// </summary>
    public string Token { get; set; } = DaprDefaults.GetDefaultAppApiToken(null);
}