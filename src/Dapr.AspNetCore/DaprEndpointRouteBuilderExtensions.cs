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
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

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
            if (endpoints is null)
            {
                throw new System.ArgumentNullException(nameof(endpoints));
            }

            return endpoints.MapGet("dapr/subscribe", async context =>
            {
                var dataSource = context.RequestServices.GetRequiredService<EndpointDataSource>();
                var entries = dataSource.Endpoints
                    .OfType<RouteEndpoint>()
                    .Where(e => e.Metadata.GetMetadata<TopicAttribute>()?.Name != null)   // only endpoints which have  TopicAttribute with not null Name.
                    .Where(e => e.RoutePattern.Parameters.Count == 0) // only endpoints which don't have parameters.
                    .Distinct()
                    .Select(e => (e.Metadata.GetMetadata<TopicAttribute>().Name, e.RoutePattern.RawText));

                context.Response.ContentType = "application/json";
                using Utf8JsonWriter writer = new Utf8JsonWriter(context.Response.BodyWriter);
                writer.WriteStartArray();

                foreach (var entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("topic", entry.Name);
                    writer.WriteString("route", entry.RawText);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                await writer.FlushAsync();
            });
        }
    }
}
