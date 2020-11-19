// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using Dapr.Actors.Runtime;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    internal static class EndpointRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder AddDaprConfigRoute(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapGet("dapr/config", async context =>
            {
                context.Response.ContentType = "application/json";
                await runtime.SerializeSettingsAndRegisteredTypes(context.Response.BodyWriter);
            });
        }     

        public static IEndpointConventionBuilder AddActorDeactivationRoute(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapDelete("actors/{actorTypeName}/{actorId}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                await runtime.DeactivateAsync(actorTypeName, actorId);
            });
        }

        public static IEndpointConventionBuilder AddActorMethodRoute(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapPut("actors/{actorTypeName}/{actorId}/method/{methodName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var methodName = (string)routeValues["methodName"];

                // If Header is present, call is made using Remoting, use Remoting dispatcher.
                if (context.Request.Headers.ContainsKey(Constants.RequestHeaderName))
                {
                    var daprActorheader = context.Request.Headers[Constants.RequestHeaderName];
                    var (header, body) = await runtime.DispatchWithRemotingAsync(actorTypeName, actorId, methodName, daprActorheader, context.Request.Body);

                    // Item 1 is header , Item 2 is body
                    if (header != string.Empty)
                    {
                        // exception case
                        context.Response.Headers.Add(Constants.ErrorResponseHeaderName, header); // add error header
                    }

                    await context.Response.Body.WriteAsync(body, 0, body.Length); // add response message body
                }
                else
                {
                    // write exception info in response.
                    try
                    {
                        await runtime.DispatchWithoutRemotingAsync(actorTypeName, actorId, methodName, context.Request.Body, context.Response.Body);
                    }
                    catch (Exception e)
                    {
                        context.Response.Headers.Add("Connection: close", default(string));
                        context.Response.ContentType = "application/json";
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsync(e.ToString());
                        await context.Response.CompleteAsync();
                    }
                }
            });
        }

        public static IEndpointConventionBuilder AddReminderRoute(this IEndpointRouteBuilder endpoints)
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
            });
        }

        public static IEndpointConventionBuilder AddTimerRoute(this IEndpointRouteBuilder endpoints)
        {
            var runtime = endpoints.ServiceProvider.GetRequiredService<ActorRuntime>();
            return endpoints.MapPut("actors/{actorTypeName}/{actorId}/method/timer/{timerName}", async context =>
            {
                // context.Request.EnableBuffering();
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var timerName = (string)routeValues["timerName"];

                // read dueTime, period and data from Request Body.
                await runtime.FireTimerAsync(actorTypeName, actorId, timerName, context.Request.Body);
            });
        }
    }
}
