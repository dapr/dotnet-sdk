// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System.Linq;
    using System.Text.Json;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Dapr;
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
                    .Select(e => e.Metadata.GetMetadata<TopicAttribute>()?.Name)
                    .Where(n => n != null)
                    .Distinct()
                    .ToArray();

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, entries, context.RequestServices.GetService<JsonSerializerOptions>());
            });
        }
    }
}