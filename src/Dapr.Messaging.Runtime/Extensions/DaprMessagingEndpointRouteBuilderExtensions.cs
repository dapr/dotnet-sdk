// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging;

/// <summary>
/// Provides extension methods to map Dapr messaging endpoints on the ASP.NET Core endpoint routing pipeline.
/// </summary>
public static class DaprMessagingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Automatically discovers registered Dapr messaging subscribers and maps the necessary endpoints
    /// (HTTP subscriptions, discovery routes, and/or gRPC AppCallback service) based on the configured delivery modes.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapDaprMessaging(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry = endpoints.ServiceProvider.GetRequiredService<IDaprMessagingSubscriberRegistry>();

        var hasHttp = registry.Descriptors.Any(d => d.Delivery == DeliveryMode.Http);
        var hasProgrammatic = registry.Descriptors.Any(d => d.Delivery == DeliveryMode.Programmatic);

        if (hasHttp)
        {
            endpoints.MapDaprHttpSubscriptions();
        }

        if (hasProgrammatic)
        {
            endpoints.MapDaprAppCallback();
        }

        return endpoints;
    }
}
