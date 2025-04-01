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

namespace Microsoft.Extensions.DependencyInjection;

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
    /// <param name="lifetime">The lifetime of the registered services.</param>
    public static void AddActors(this IServiceCollection? services, Action<ActorRuntimeOptions>? configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        // Routing, health checks and logging are required dependencies.
        services.AddRouting();
        services.AddHealthChecks();
        services.AddLogging();

        var actorRuntimeRegistration = new Func<IServiceProvider, ActorRuntime>(s =>
        {
            var options = s.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;
            ConfigureActorOptions(s, options);
                
            var loggerFactory = s.GetRequiredService<ILoggerFactory>();
            var activatorFactory = s.GetRequiredService<ActorActivatorFactory>();
            var proxyFactory = s.GetRequiredService<IActorProxyFactory>();
            return new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);
        });
        var proxyFactoryRegistration = new Func<IServiceProvider, IActorProxyFactory>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;
            ConfigureActorOptions(serviceProvider, options);

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

        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                services.TryAddScoped<ActorActivatorFactory, DependencyInjectionActorActivatorFactory>();
                services.TryAddScoped<ActorRuntime>(actorRuntimeRegistration);
                services.TryAddScoped<IActorProxyFactory>(proxyFactoryRegistration);
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<ActorActivatorFactory, DependencyInjectionActorActivatorFactory>();
                services.TryAddTransient<ActorRuntime>(actorRuntimeRegistration);
                services.TryAddTransient<IActorProxyFactory>(proxyFactoryRegistration);
                break;
            default:
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<ActorActivatorFactory, DependencyInjectionActorActivatorFactory>();
                services.TryAddSingleton<ActorRuntime>(actorRuntimeRegistration);
                services.TryAddSingleton<IActorProxyFactory>(proxyFactoryRegistration);
                break;
        }
            
        if (configure != null)
        {
            services.Configure<ActorRuntimeOptions>(configure);
        }
    }
        
    private static void ConfigureActorOptions(IServiceProvider serviceProvider, ActorRuntimeOptions options)
    {
        var configuration = serviceProvider.GetService<IConfiguration>();
        options.DaprApiToken = !string.IsNullOrWhiteSpace(options.DaprApiToken)
            ? options.DaprApiToken
            : DaprDefaults.GetDefaultDaprApiToken(configuration);
        options.HttpEndpoint = !string.IsNullOrWhiteSpace(options.HttpEndpoint)
            ? options.HttpEndpoint
            : DaprDefaults.GetDefaultHttpEndpoint();
    }
}