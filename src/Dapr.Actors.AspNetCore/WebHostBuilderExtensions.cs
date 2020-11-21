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
        /// <param name="configure">A delegate used to register actors and configure the actor runtime.</param>
        /// <returns>The Microsoft.AspNetCore.Hosting.IWebHostBuilder.</returns>
        public static IWebHostBuilder UseActors(this IWebHostBuilder hostBuilder, Action<ActorRuntimeOptions> configure)
        {
            if (hostBuilder == null)
            {
                throw new ArgumentNullException("hostBuilder");
            }

            hostBuilder.ConfigureServices(services =>
            {
                if (configure != null)
                {
                    services.Configure<ActorRuntimeOptions>(configure);
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
                services.AddSingleton<ActorActivatorFactory, DependencyInjectionActorActivatorFactory>();

                services.AddSingleton<ActorRuntime>(s =>
                {   
                    var options = s.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;
                    var loggerFactory = s.GetRequiredService<ILoggerFactory>();
                    var activatorFactory = s.GetRequiredService<ActorActivatorFactory>();
                    return new ActorRuntime(options, loggerFactory, activatorFactory);
                });
            });

            return hostBuilder;
        }
    }
}
