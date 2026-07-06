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

namespace Microsoft.AspNetCore.Authorization;

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