// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Dapr;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Patterns;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Contains extension methods for <see cref="IEndpointRouteBuilder" />.
    /// </summary>
    public static class DaprEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps an endpoint that will respond to requests to <c>/dapr/subscribe</c> from the
        /// Dapr runtime.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder" />.</returns>
        public static IEndpointConventionBuilder MapSubscribeHandler(this IEndpointRouteBuilder endpoints)
        {
            return CreateSubscribeEndPoint(endpoints);
        }

        /// <summary>
        /// Maps an endpoint that will respond to requests to <c>/dapr/subscribe</c> from the
        /// Dapr runtime.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <param name="options">Configuration options</param>
        /// <returns>The <see cref="IEndpointConventionBuilder" />.</returns>
        /// <seealso cref="MapSubscribeHandler(IEndpointRouteBuilder)"/>
        public static IEndpointConventionBuilder MapSubscribeHandler(this IEndpointRouteBuilder endpoints, SubscribeOptions options)
        {
            return CreateSubscribeEndPoint(endpoints, options);
        }

        private static IEndpointConventionBuilder CreateSubscribeEndPoint(IEndpointRouteBuilder endpoints, SubscribeOptions options = null)
        {
            if (endpoints is null)
            {
                throw new System.ArgumentNullException(nameof(endpoints));
            }

            return endpoints.MapGet("dapr/subscribe", async context =>
            {
                var dataSource = context.RequestServices.GetRequiredService<EndpointDataSource>();
                var entries = dataSource.Endpoints
                    .OfType<RouteEndpoint>()
                    .Where(e => e.Metadata.GetMetadata<ITopicMetadata>()?.Name != null)   // only endpoints which have  TopicAttribute with not null Name.
                    .Distinct()
                    .Select(e => (e.Metadata.GetMetadata<ITopicMetadata>().PubsubName, e.Metadata.GetMetadata<ITopicMetadata>().Name, e.Metadata.GetMetadata<IRawTopicMetadata>()?.EnableRawPayload, e.RoutePattern));


                var logger = context.RequestServices.GetService<ILoggerFactory>().CreateLogger("DaprTopicSubscription");

                List<Subscription> subscriptions = new();
                foreach (var entry in entries)
                {
                    // only return topics which have routes without parameters.
                    if (entry.RoutePattern.Parameters.Count > 0)
                    {
                        if (logger != null)
                        {
                            logger.LogError("Topic subscription doesn't support route with parameters. Subscription for topic {name} is removed.", entry.Name);
                        }

                        continue;
                    }

                    var route = string.Join("/",
                        entry.RoutePattern.PathSegments
                        .Select(segment => string.Concat(segment.Parts.Cast<RoutePatternLiteralPart>()
                        .Select(part => part.Content))));

                    var rawPayload = entry.EnableRawPayload ?? options?.EnableRawPayload;
                    var subscription = new Subscription
                    {
                        Topic = entry.Name,
                        PubsubName = entry.PubsubName,
                        Route = route
                    };

                    if (rawPayload != null)
                    {
                        subscription.Metadata = new Metadata
                        {
                            RawPayload = rawPayload.ToString().ToLower()
                        };
                    }
                    subscriptions.Add(subscription);
                }

                await context.Response.WriteAsync(JsonSerializer.Serialize(subscriptions,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }));
            });
        }
    }
}
