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
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

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
        /// <param name="runtimeOptions">Options to configure Actor runtime..</param>
        /// <returns>The Microsoft.AspNetCore.Hosting.IWebHostBuilder.</returns>
        public static IWebHostBuilder UseActors(this IWebHostBuilder hostBuilder, Action<DaprActorRuntimeOptions> runtimeOptions)
        {
            if (hostBuilder == null)
            {
                throw new ArgumentNullException("hostBuilder");
            }

            hostBuilder.ConfigureServices(services =>
            {
                if (runtimeOptions != null)
                {
                    services.Configure<DaprActorRuntimeOptions>(runtimeOptions);
                }
            });

            // Check if 'UseActors' has already been called.
            if (hostBuilder.GetSetting(SettingName) != null && hostBuilder.GetSetting(SettingName).Equals(true.ToString(), StringComparison.Ordinal))
            {
                return hostBuilder;
            }

            // Set flag to prevent double service configuration
            hostBuilder.UseSetting(SettingName, true.ToString());

            hostBuilder.ConfigureServices(services =>
            {
                // Add routes.
                services.AddRouting();
                services.AddHealthChecks();
                services.AddSingleton<IStartupFilter>(new DaprActorSetupFilter());

                services.AddSingleton<ActorRuntime>(s =>
                {
                    return new ActorRuntime(s.GetRequiredService<IOptions<DaprActorRuntimeOptions>>().Value, s.GetRequiredService<ILoggerFactory>());
                });
            });

            hostBuilder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", 
                    optional: true, 
                    reloadOnChange: true);
            });
            return hostBuilder;
        }
    }
}
