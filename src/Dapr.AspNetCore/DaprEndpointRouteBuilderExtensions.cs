// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr;
    using Dapr.AspNetCore;
    using Dapr.Client;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;
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
        /// Adds Dapr integration for Minimal APIs to the provided <see cref="IEndpointRouteBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <param name="services">The <see cref="IServiceCollection" />.</param>
        /// <param name="configureClient">The (optional) <see cref="DaprClientBuilder" /> to use for configuring the DaprClient.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder" /> builder.</returns>
        public static IEndpointRouteBuilder AddDapr(this IEndpointRouteBuilder builder, IServiceCollection services, Action<DaprClientBuilder> configureClient = null)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // This pattern prevents registering services multiple times in the case AddDapr is called
            // by non-user-code.
            if (services.Any(s => s.ImplementationType == typeof(DaprMinimalApisMarkerService)))
            {
                return builder;
            }

            services.AddDaprClient(configureClient);

            services.AddSingleton<DaprMinimalApisMarkerService>();
            services.AddSingleton<IApplicationModelProvider, StateEntryApplicationModelProvider>();
            services.Configure<MvcOptions>(options =>
            {
                options.ModelBinderProviders.Insert(0, new StateEntryModelBinderProvider());
            });

            return builder;
        }

        /// <summary>
        /// Maps an endpoint that will respond to requests to <c>/dapr/subscribe</c> from the
        /// Dapr runtime.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder" />.</returns>
        public static IEndpointConventionBuilder MapSubscribeHandler(this IEndpointRouteBuilder endpoints)
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
                    .Select(e => (e.Metadata.GetMetadata<ITopicMetadata>().PubsubName, e.Metadata.GetMetadata<ITopicMetadata>().Name, e.RoutePattern));

                context.Response.ContentType = "application/json";
                using var writer = new Utf8JsonWriter(context.Response.BodyWriter);
                writer.WriteStartArray();

                var logger = context.RequestServices.GetService<ILoggerFactory>().CreateLogger("DaprTopicSubscription");
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

                    writer.WriteStartObject();
                    writer.WriteString("topic", entry.Name);

                    var route = string.Join("/",
                        entry.RoutePattern.PathSegments
                        .Select(segment => string.Concat(segment.Parts.Cast<RoutePatternLiteralPart>()
                        .Select(part => part.Content))));

                    writer.WriteString("route", route);
                    writer.WriteString("pubsubName", entry.PubsubName);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                await writer.FlushAsync();
            });
        }

        private class DaprMinimalApisMarkerService
        {
        }
    }
}
