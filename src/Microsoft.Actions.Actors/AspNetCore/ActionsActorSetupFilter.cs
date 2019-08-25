// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Routing;

    internal class ActionsActorSetupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            // Adds routes for Actors interaction.
            return app =>
            {
                var actorRouteBuilder = new RouteBuilder(app);
                actorRouteBuilder.AddActionsConfigRoute();
                actorRouteBuilder.AddGetSupportedActorTypesRoute();
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
