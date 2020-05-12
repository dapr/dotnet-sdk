// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
                    endpoints.MapHealthChecks("/healthz");
                    endpoints.AddDaprConfigRoute();
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
