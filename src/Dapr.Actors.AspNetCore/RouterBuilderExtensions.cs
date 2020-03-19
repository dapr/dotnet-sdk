// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using System.Buffers;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Actors.Runtime;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    internal static class RouterBuilderExtensions
    {
        public static void AddDaprConfigRoute(this IRouteBuilder routeBuilder, ActorRuntime actorRuntime)
        {
            routeBuilder.MapGet("dapr/config", async context =>
            {
                await WriteSupportedActorTypesAsJsonAsync(context.Response.BodyWriter, actorRuntime);
            });
        }

        public static void AddActorActivationRoute(this IRouteBuilder routeBuilder, ActorRuntime actorRuntime)
        {
            routeBuilder.MapPost("actors/{actorTypeName}/{actorId}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                await actorRuntime.ActivateAsync(actorTypeName, actorId);
            });
        }

        public static void AddActorDeactivationRoute(this IRouteBuilder routeBuilder, ActorRuntime actorRuntime)
        {
            routeBuilder.MapDelete("actors/{actorTypeName}/{actorId}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                await actorRuntime.DeactivateAsync(actorTypeName, actorId);
            });
        }

        public static void AddActorMethodRoute(this IRouteBuilder routeBuilder, ActorRuntime actorRuntime)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/{methodName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var methodName = (string)routeValues["methodName"];

                // If Header is present, call is made using Remoting, use Remoting dispatcher.
                if (context.Request.Headers.ContainsKey(Constants.RequestHeaderName))
                {
                    var daprActorheader = context.Request.Headers[Constants.RequestHeaderName];
                    var (header, body) = await actorRuntime.DispatchWithRemotingAsync(actorTypeName, actorId, methodName, daprActorheader, context.Request.Body);

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
                        await actorRuntime.DispatchWithoutRemotingAsync(actorTypeName, actorId, methodName, context.Request.Body, context.Response.Body);
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

        public static void AddReminderRoute(this IRouteBuilder routeBuilder, ActorRuntime actorRuntime)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/remind/{reminderName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var reminderName = (string)routeValues["reminderName"];

                // read dueTime, period and data from Request Body.
                await actorRuntime.FireReminderAsync(actorTypeName, actorId, reminderName, context.Request.Body);
            });
        }

        public static void AddTimerRoute(this IRouteBuilder routeBuilder, ActorRuntime actorRuntime)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/timer/{timerName}", async context =>
            {
                var routeValues = context.Request.RouteValues;
                var actorTypeName = (string)routeValues["actorTypeName"];
                var actorId = (string)routeValues["actorId"];
                var timerName = (string)routeValues["timerName"];

                // read dueTime, period and data from Request Body.
                await actorRuntime.FireTimerAsync(actorTypeName, actorId, timerName);
            });
        }

        private static async Task WriteSupportedActorTypesAsJsonAsync(
            IBufferWriter<byte> output,
            ActorRuntime actorRuntime)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();
            writer.WritePropertyName("entities");
            writer.WriteStartArray();

            foreach (var actorType in actorRuntime.RegisteredActorTypes)
            {
                writer.WriteStringValue(actorType);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync();
        }
    }
}
