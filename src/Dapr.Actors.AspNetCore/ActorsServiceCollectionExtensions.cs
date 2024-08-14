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

#nullable enable

using System;
using Dapr;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Configuration;
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
        public static void AddActors(this IServiceCollection? services, Action<ActorRuntimeOptions>? configure)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));

            // Routing and health checks are required dependencies.
            services.AddRouting();
            services.AddHealthChecks();

            services.TryAddSingleton<ActorActivatorFactory, DependencyInjectionActorActivatorFactory>();
            services.TryAddSingleton<ActorRuntime>(s =>
            {   
                var options = s.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;

                //Replace the HttpEndpoint with an endpoint prioritizing IConfiguration
                var configuration = s.GetService<IConfiguration>();
                options.HttpEndpoint = options.HttpEndpoint != "http://127.0.0.1:3500"
                    ? GetHttpEndpoint(configuration)
                    : options.HttpEndpoint;
                options.DaprApiToken = string.IsNullOrWhiteSpace(options.DaprApiToken)
                    ? GetApiToken(configuration)
                    : options.DaprApiToken;

                var loggerFactory = s.GetRequiredService<ILoggerFactory>();
                var activatorFactory = s.GetRequiredService<ActorActivatorFactory>();
                var proxyFactory = s.GetRequiredService<IActorProxyFactory>();
                return new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);
            });

            services.TryAddSingleton<IActorProxyFactory>(s =>
            {
                var options = s.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;

                //Replace the HttpEndpoint with an endpoint prioritizing IConfiguration
                var configuration = s.GetService<IConfiguration>();
                options.HttpEndpoint = options.HttpEndpoint != "http://127.0.0.1:3500"
                    ? GetHttpEndpoint(configuration)
                    : options.HttpEndpoint;
                options.DaprApiToken = string.IsNullOrWhiteSpace(options.DaprApiToken)
                    ? GetApiToken(configuration)
                    : options.DaprApiToken;

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

        /// <summary>
        /// Retrieves the Dapr API token using a failover approach starting with an optional <see cref="IConfiguration"/>
        /// instance, then trying to pull from the well-known environment variable name and then opting for an empty string
        /// as a default value.
        /// </summary>
        /// <returns>The Dapr API token.</returns>
        private static string GetApiToken(IConfiguration? configuration) => GetResourceValue(configuration, DaprDefaults.DaprApiTokenName);

        /// <summary>
        /// Builds the Dapr gRPC endpoint using the value from the IConfiguration, if available, then falling back
        /// to the value in the environment variable(s) and finally otherwise using the default value (an empty string).
        /// </summary>
        /// <remarks>
        /// Marked as internal for testing purposes.
        /// </remarks>
        /// <param name="configuration">An injected instance of the <see cref="IConfiguration"/>.</param>
        /// <returns>The built gRPC endpoint.</returns>
        private static string GetHttpEndpoint(IConfiguration? configuration)
        {
            //Prioritize pulling from IConfiguration with a fallback from pulling from the environment variable directly
            var httpEndpoint = GetResourceValue(configuration, DaprDefaults.DaprHttpEndpointName);
            var httpPort = GetResourceValue(configuration, DaprDefaults.DaprHttpPortName);
            int? parsedGrpcPort = string.IsNullOrWhiteSpace(httpPort) ? null : int.Parse(httpPort);

            var endpoint = BuildEndpoint(httpEndpoint, parsedGrpcPort);
            return string.IsNullOrWhiteSpace(endpoint) ? $"http://localhost:{DaprDefaults.DefaultHttpPort}/" : endpoint;
        }

        /// <summary>
        /// Retrieves the specified value prioritizing pulling it from <see cref="IConfiguration"/>, falling back
        /// to an environment variable, and using an empty string as a default.
        /// </summary>
        /// <param name="configuration">An instance of an <see cref="IConfiguration"/>.</param>
        /// <param name="name">The name of the value to retrieve.</param>
        /// <returns>The value of the resource.</returns>
        private static string GetResourceValue(IConfiguration? configuration, string name)
        {
            //Attempt to retrieve first from the configuration
            var configurationValue = configuration?.GetValue<string?>(name);
            if (configurationValue is not null)
                return configurationValue;

            //Fall back to the environment variable with the same name or default to an empty string
            var envVar = Environment.GetEnvironmentVariable(name);
            return envVar ?? string.Empty;
        }

        /// <summary>
        /// Builds the endpoint provided an optional endpoint and optional port.
        /// </summary>
        /// <remarks>
        /// Marked as internal for testing purposes.
        /// </remarks>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="endpointPort">The port</param>
        /// <returns>A constructed endpoint value.</returns>
        internal static string BuildEndpoint(string? endpoint, int? endpointPort)
        {
            if (string.IsNullOrWhiteSpace(endpoint) && endpointPort is null)
                return string.Empty;

            var endpointBuilder = new UriBuilder();
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                //Extract the scheme, host and port from the endpoint
                var uri = new Uri(endpoint);
                endpointBuilder.Scheme = uri.Scheme;
                endpointBuilder.Host = uri.Host;
                endpointBuilder.Port = uri.Port;

                //Update the port if provided separately
                if (endpointPort is not null)
                    endpointBuilder.Port = (int)endpointPort;
            }
            else if (string.IsNullOrWhiteSpace(endpoint) && endpointPort is not null)
            {
                endpointBuilder.Host = "localhost";
                endpointBuilder.Port = (int)endpointPort;
            }

            return endpointBuilder.ToString();
        }
    }
}
