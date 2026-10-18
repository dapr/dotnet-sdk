// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// ------------------------------------------------------------------------

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Dapr.Messaging.Subscribe.Http;

namespace Dapr.Messaging;

/// <summary>
/// Maps the HTTP subscription discovery and delivery endpoints used by Dapr pub/sub.
/// </summary>
public static class DaprHttpSubscriptionApplicationBuilderExtensions
{
    /// <summary>
    /// Maps <c>/dapr/subscribe</c> and the generated HTTP topic routes.
    /// </summary>
    public static IEndpointConventionBuilder MapDaprHttpSubscriptions(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var discoveryEndpoint = endpoints.MapGet("dapr/subscribe", DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync);

        var registry = endpoints.ServiceProvider.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        foreach (var descriptor in registry.Descriptors.Where(d => d.Delivery == DeliveryMode.Http))
        {
            var route = descriptor.Route.TrimStart('/');
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new InvalidOperationException(
                    $"The HTTP subscription route for '{descriptor.PubsubName}/{descriptor.TopicName}' cannot be empty.");
            }

            endpoints.MapPost(route, context =>
                DaprHttpSubscriptionEndpoint.HandleEventAsync(context, descriptor));
        }

        return discoveryEndpoint;
    }

}
