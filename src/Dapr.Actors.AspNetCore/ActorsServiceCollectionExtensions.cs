// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for using Dapr Actors with dependency injection.
    /// </summary>
    public static class ActorsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Dapr Actors support to the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" />.</param>
        /// <param name="configure">A delegate used to configure actor options and register actor types.</param>
        public static void AddActors(this IServiceCollection services, Action<ActorRuntimeOptions> configure)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // Routing and health checks are required dependencies.
            services.AddRouting();
            services.AddHealthChecks();

            services.TryAddSingleton<ActorActivatorFactory, DependencyInjectionActorActivatorFactory>();
            services.TryAddSingleton<ActorRuntime>(s =>
            {   
                var options = s.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;
                var loggerFactory = s.GetRequiredService<ILoggerFactory>();
                var activatorFactory = s.GetRequiredService<ActorActivatorFactory>();
                var proxyFactory = s.GetRequiredService<IActorProxyFactory>();
                return new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);
            });

            services.TryAddSingleton<IActorProxyFactory>(s =>
            {
                var options = s.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;
                var factory = new ActorProxyFactory() 
                { 
                    DefaultOptions =
                    {
                        JsonSerializerOptions = options.JsonSerializerOptions,
                        DaprApiToken = options.DaprApiToken,
                        HttpEndpoint = options.HttpEndpoint,
                    }
                };

                return factory;
            });

            if (configure != null)
            {
                services.Configure<ActorRuntimeOptions>(configure);
            }
        }
    }
}
