// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using Dapr.Actors.Runtime;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Routing;

    internal class DaprActorSetupFilter : IStartupFilter
    {
        private readonly ActorRuntime actorRuntime;

        public DaprActorSetupFilter(ActorRuntime actorRuntime)
        {
            this.actorRuntime = actorRuntime;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            // Adds routes for Actors interaction.
            return app =>
            {
                var actorRouteBuilder = new RouteBuilder(app);
                actorRouteBuilder.AddDaprConfigRoute(actorRuntime);
                actorRouteBuilder.AddActorActivationRoute(actorRuntime);
                actorRouteBuilder.AddActorDeactivationRoute(actorRuntime);
                actorRouteBuilder.AddActorMethodRoute(actorRuntime);
                actorRouteBuilder.AddReminderRoute(actorRuntime);
                actorRouteBuilder.AddTimerRoute(actorRuntime);

                app.UseRouter(actorRouteBuilder.Build());
                next(app);
            };
        }
    }
}
