// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.AspNetCore
{
    using System.Linq;
    using Microsoft.Actions.Actors.Runtime;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Newtonsoft.Json.Linq;

    internal static class RouterBuilderExtensions
    {
        public static void AddActionsConfigRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapGet("actions/config", (request, response, routeData) =>
            {
                var result = new JObject(
                    new JProperty("entities", new JArray(ActorRuntime.RegisteredActorTypes.Select(actorType => new JValue(actorType)))));

                return response.WriteAsync(result.ToString());
            });
        }

        public static void AddGetSupportedActorTypesRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapGet("actors", (request, response, routeData) =>
            {
                var result = new JObject(
                    new JProperty("entities", new JArray(ActorRuntime.RegisteredActorTypes.Select(actorType => new JValue(actorType)))));

                return response.WriteAsync(result.ToString());
            });
        }

        public static void AddActorActivationRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPost("actors/{actorTypeName}/{actorId}", (request, response, routeData) =>
            {
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                return ActorRuntime.ActivateAsync(actorTypeName, actorId);
            });
        }

        public static void AddActorDeactivationRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapDelete("actors/{actorTypeName}/{actorId}", (request, response, routeData) =>
            {
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                return ActorRuntime.DeactivateAsync(actorTypeName, actorId);
            });
        }

        public static void AddActorMethodRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/{methodName}", (request, response, routeData) =>
            {
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                var methodName = (string)routeData.Values["methodName"];

                // If Header is present, call is made using Remoting, use Remoting dispatcher.
                if (request.Headers.ContainsKey(Constants.RequestHeaderName))
                {
                    var actionsActorheader = request.Headers[Constants.RequestHeaderName];
                    return ActorRuntime.DispatchWithRemotingAsync(actorTypeName, actorId, methodName, actionsActorheader, request.Body)
                        .ContinueWith(async t =>
                        {
                            var result = t.GetAwaiter().GetResult();

                            // Item 1 is header , Item 2 is body
                            response.Headers.Add(Constants.ErrorResponseHeaderName, result.Item1); // add header
                            await response.Body.WriteAsync(result.Item2, 0, result.Item2.Length); // add response message body
                        });
                }
                else
                {
                    return ActorRuntime.DispatchWithoutRemotingAsync(actorTypeName, actorId, methodName, request.Body, response.Body);
                }
            });
        }

        public static void AddReminderRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/remind/{reminderName}", (request, response, routeData) =>
            {
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                var reminderName = (string)routeData.Values["reminderName"];

                // read dueTime, period and data from Request Body.
                return ActorRuntime.FireReminderAsync(actorTypeName, actorId, reminderName, request.Body);
            });
        }

        public static void AddTimerRoute(this IRouteBuilder routeBuilder)
        {
            routeBuilder.MapPut("actors/{actorTypeName}/{actorId}/method/timer/{timerName}", (request, response, routeData) =>
            {
                var actorTypeName = (string)routeData.Values["actorTypeName"];
                var actorId = (string)routeData.Values["actorId"];
                var timerName = (string)routeData.Values["timerName"];

                // read dueTime, period and data from Request Body.
                return ActorRuntime.FireTimerAsync(actorTypeName, actorId, timerName);
            });
        }
    }
}
