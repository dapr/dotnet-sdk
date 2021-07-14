// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.AspNetCore.Builder
{
    using System;
    using Dapr.Actors;
    using Dapr.Actors.Runtime;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Contains extension methods for using Dapr Actors with endpoint routing.
    /// </summary>
    public static class ActorsEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps endpoints for Dapr Actors into the <see cref="IEndpointRouteBuilder" />.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder" />.</param>
        /// <returns>
        /// An <see cref="IEndpointConventionBuilder" /> that can be used to configure the endpoints.
        /// </returns>
        public static IEndpointConventionBuilder MapActorsHandlers(this IEndpointRouteBuilder endpoints)
        {
            if (endpoints.ServiceProvider.GetService<ActorRuntime>() is null)
            {
                throw new InvalidOperationException(
                    "The ActorRuntime service is not registered with the dependency injection container. " + 
                    "Call AddActors() inside ConfigureServices() to register the actor runtime and actor types.");
            }

            var builders = new []
            {
                MapDaprConfigEndpoint(endpoints),
                MapActorDeactivationEndpoint(endpoints),
                MapActorMethodEndpoint(endpoints),
                MapReminderEndpoint(endpoints),
                MapTimerEndpoint(endpoints),
                MapActorHealthChecks(endpoints),
            };

            return new CompositeEndpointConventionBuilder(builders);
        }

        private static IEndpointConventionBuilder MapDaprConfigEndpoint(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapGet("dapr/config", async context =>
            {
                context.Response.ContentType = "application/json";
                await runtime.SerializeSettingsAndRegisteredTypes(context.Response.BodyWriter);
            }).WithDisplayName(b => "Dapr Actors Config");
        }     

        private static IEndpointConventionBuilder MapActorDeactivationEndpoint(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapDelete("actors/{actorTypeName}/{actorId}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                await runtime.DeactivateAsync(actorTypeName, actorId);
            }).WithDisplayName(b => "Dapr Actors Deactivation");
        }

        private static IEndpointConventionBuilder MapActorMethodEndpoint(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapPut("actors/{actorTypeName}/{actorId}/method/{methodName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var methodName = (string)routeValues["methodName"];

                if (context.Request.Headers.ContainsKey(Constants.ReentrancyRequestHeaderName))
                {
                    var daprReentrancyHeader = context.Request.Headers[Constants.ReentrancyRequestHeaderName];
                    ActorReentrancyContextAccessor.ReentrancyContext = daprReentrancyHeader;
                }

                // If Header is present, call is made using Remoting, use Remoting dispatcher.
                if (context.Request.Headers.ContainsKey(Constants.RequestHeaderName))
                {
                    var daprActorheader = context.Request.Headers[Constants.RequestHeaderName];

                    try
                    {
                        var (header, body) = await runtime.DispatchWithRemotingAsync(actorTypeName, actorId, methodName, daprActorheader, context.Request.Body);

                        // Item 1 is header , Item 2 is body
                        if (header != string.Empty)
                        {
                            // exception case
                            context.Response.Headers.Add(Constants.ErrorResponseHeaderName, header); // add error header
                        }

                        await context.Response.Body.WriteAsync(body, 0, body.Length); // add response message body
                        }
                    finally
                    {
                        ActorReentrancyContextAccessor.ReentrancyContext = null;
                    }                    
                }
                else
                {
                    try
                    {
                        await runtime.DispatchWithoutRemotingAsync(actorTypeName, actorId, methodName, context.Request.Body, context.Response.Body);
                    }
                    finally
                    {
                        ActorReentrancyContextAccessor.ReentrancyContext = null;
                    }
                }
            }).WithDisplayName(b => "Dapr Actors Invoke");
        }

        private static IEndpointConventionBuilder MapReminderEndpoint(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapPut("actors/{actorTypeName}/{actorId}/method/remind/{reminderName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var reminderName = (string)routeValues["reminderName"];

                // read dueTime, period and data from Request Body.
                await runtime.FireReminderAsync(actorTypeName, actorId, reminderName, context.Request.Body);
            }).WithDisplayName(b => "Dapr Actors Reminder");
        }

        private static IEndpointConventionBuilder MapTimerEndpoint(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapPut("actors/{actorTypeName}/{actorId}/method/timer/{timerName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var timerName = (string)routeValues["timerName"];

                // read dueTime, period and data from Request Body.
                await runtime.FireTimerAsync(actorTypeName, actorId, timerName, context.Request.Body);
            }).WithDisplayName(b => "Dapr Actors Timer");
        }


        private static IEndpointConventionBuilder MapActorHealthChecks(this IEndpointRouteBuilder endpoints)
        {
            var builder = endpoints.MapHealthChecks("/healthz");
            builder.Add(b => 
            {
                // Sets the route order so that this is matched with lower priority than an endpoint
                // configured by default.
                //
                // This is necessary because it allows a user defined `/healthz` endpoint to win in the
                // most common cases, but still fulfills Dapr's contract when the user doesn't have
                // a health check at `/healthz`.
                ((RouteEndpointBuilder)b).Order = 100;
            });
            return builder.WithDisplayName(b => "Dapr Actors Health Check");
        }

        private class CompositeEndpointConventionBuilder : IEndpointConventionBuilder
        {
            private readonly IEndpointConventionBuilder[] inner;

            public CompositeEndpointConventionBuilder(IEndpointConventionBuilder[] inner)
            {
                this.inner = inner;
            }

            public void Add(Action<EndpointBuilder> convention)
            {
                for (var i = 0; i < inner.Length; i++)
                {
                    inner[i].Add(convention);
                }
            }
        }
    }
}
