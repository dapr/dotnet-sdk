// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Routing;

    internal class DaprActorSetupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            // Adds routes for Actors interaction.
            return app =>
            {
                var actorRouteBuilder = new RouteBuilder(app);
                actorRouteBuilder.AddDaprConfigRoute();
                actorRouteBuilder.AddActorActivationRoute();
                actorRouteBuilder.AddActorDeactivationRoute();
                actorRouteBuilder.AddActorMethodRoute();
                actorRouteBuilder.AddReminderRoute();
                actorRouteBuilder.AddTimerRoute();

                app.UseRouter(actorRouteBuilder.Build());
                next(app);
            };
        }
    }
}
