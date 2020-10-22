// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore
{
    using System;
    using Dapr.Actors.Runtime;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Class containing DaprActor related extension methods for Microsoft.AspNetCore.Hosting.IWebHostBuilder.
    /// </summary>
    public static class WebHostBuilderExtensions
    {
        private static readonly string SettingName = "UseDaprActors";

        /// <summary>
        /// Configures the service to use the routes needed by Dapr Actor runtime.
        /// </summary>
        /// <param name="hostBuilder">The Microsoft.AspNetCore.Hosting.IWebHostBuilder to configure.</param>
        /// <param name="configureActorRuntime">Adds a delegate to configure Actor runtime..</param>
        /// <returns>The Microsoft.AspNetCore.Hosting.IWebHostBuilder.</returns>
        public static IWebHostBuilder UseActors(this IWebHostBuilder hostBuilder, Action<ActorRuntime> configureActorRuntime)
        {
            if (hostBuilder == null)
            {
                throw new ArgumentNullException("hostBuilder");
            }

            // Check if 'UseActors' has already been called.
            if (hostBuilder.GetSetting(SettingName) != null && hostBuilder.GetSetting(SettingName).Equals(true.ToString(), StringComparison.Ordinal))
            {
                return hostBuilder;
            }

            var runtime = new ActorRuntime();
            if (configureActorRuntime != null)
            {
                configureActorRuntime.Invoke(runtime);
            }

            // Set flag to prevent double service configuration
            hostBuilder.UseSetting(SettingName, true.ToString());

            hostBuilder.ConfigureServices(services =>
            {
                // Add routes.
                services.AddRouting();
                services.AddHealthChecks();
                services.AddSingleton<IStartupFilter>(new DaprActorSetupFilter());

                services.AddSingleton<ActorRuntime>(runtime);
            });

            return hostBuilder;
        }
    }
}
