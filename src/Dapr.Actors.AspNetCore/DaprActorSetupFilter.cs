// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;

    internal class DaprActorSetupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            // Adds routes for Actors interaction.
            return app =>
            {
                // This allows the middlewares to be configured correctly from Startup class.
                next(app);

                app.UseRouting();

                // Configure endpoints for Actors.
                app.UseEndpoints(endpoints =>
                {
                    endpoints.AddDaprConfigRoute();
                    endpoints.AddDaprHealthzRoute();
                    endpoints.AddActorActivationRoute();
                    endpoints.AddActorDeactivationRoute();
                    endpoints.AddActorMethodRoute();
                    endpoints.AddReminderRoute();
                    endpoints.AddTimerRoute();
                });
            };
        }
    }
}
