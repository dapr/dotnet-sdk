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

namespace Microsoft.AspNetCore.Builder;

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