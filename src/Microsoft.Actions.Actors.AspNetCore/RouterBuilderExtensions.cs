// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.AspNetCore
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    internal static class RouterBuilderExtensions
    {
        public static void AddActionsConfigRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapGet("actions/config", async context =>
            {
                await WriteSupportedActorTypesAsJsonAsync(context.Response.BodyWriter);
            });
        }

        public static void AddGetSupportedActorTypesRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapGet("actors", async context =>
            {
                await WriteSupportedActorTypesAsJsonAsync(context.Response.BodyWriter);
            });
        }

        public static void AddActorActivationRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPost("actors/{actorTypeName}/{actorId}", async context =>
            {
                var routeData = context.GetRouteData();
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                await ActorRuntime.ActivateAsync(actorTypeName, actorId);
            });
        }

        public static void AddActorDeactivationRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapDelete("actors/{actorTypeName}/{actorId}", async context =>
            {
                var routeData = context.GetRouteData();
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                await ActorRuntime.DeactivateAsync(actorTypeName, actorId);
            });
        }

        public static void AddActorMethodRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/{methodName}", async context =>
            {
                var routeData = context.GetRouteData();
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                var methodName = (string)routeData.Values["methodName"];

                // If Header is present, call is made using Remoting, use Remoting dispatcher.
                if (context.Request.Headers.ContainsKey(Constants.RequestHeaderName))
                {
                    var actionsActorheader = context.Request.Headers[Constants.RequestHeaderName];
                    var result = await ActorRuntime.DispatchWithRemotingAsync(actorTypeName, actorId, methodName, actionsActorheader, context.Request.Body);

                    // Item 1 is header , Item 2 is body
                    if (result.Item1 != string.Empty)
                    {
                        // exception case
                        context.Response.Headers.Add(Constants.ErrorResponseHeaderName, result.Item1); // add error header
                    }

                    await context.Response.Body.WriteAsync(result.Item2, 0, result.Item2.Length); // add response message body
                }
                else
                {
                    // write exception info in response.
                    try
                    {
                        await ActorRuntime.DispatchWithoutRemotingAsync(actorTypeName, actorId, methodName, context.Request.Body, context.Response.Body);
                    }
                    catch (Exception e)
                    {
                        await context.Response.WriteAsync(e.ToString());
                        throw;
                    }
                }
            });
        }

        public static void AddReminderRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/remind/{reminderName}", async context =>
            {
                var routeData = context.GetRouteData();
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                var reminderName = (string)routeData.Values["reminderName"];

                // read dueTime, period and data from Request Body.
                await ActorRuntime.FireReminderAsync(actorTypeName, actorId, reminderName, context.Request.Body);
            });
        }

        public static void AddTimerRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/timer/{timerName}", async context =>
            {
                var routeData = context.GetRouteData();
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                var timerName = (string)routeData.Values["timerName"];

                // read dueTime, period and data from Request Body.
                await ActorRuntime.FireTimerAsync(actorTypeName, actorId, timerName);
            });
        }

        private static async Task WriteSupportedActorTypesAsJsonAsync(IBufferWriter<byte> output)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();
            writer.WritePropertyName("entities");
            writer.WriteStartArray();

            foreach (var actorType in ActorRuntime.RegisteredActorTypes)
            {
                writer.WriteStringValue(actorType);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync();
        }
    }
}
