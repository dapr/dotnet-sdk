// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
