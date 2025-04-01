﻿// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Common.Extensions;

/// <summary>
/// Generic extension used to build out type-specific Dapr clients.
/// </summary>
internal static class DaprClientBuilderExtensions
{
    /// <summary>
    /// Registers the necessary base functionality for a Dapr client.
    /// </summary>
    /// <typeparam name="TBuilderInterface">The type of the client builder interface.</typeparam>
    /// <typeparam name="TClient">The concrete Dapr client type being created.</typeparam>
    /// <typeparam name="TClientBuilder">The type of the static builder used to build the Dapr ot client.</typeparam>
    /// <param name="services">The collection of services to which the Dapr client and associated services are being registered.</param>
    /// <param name="configure">An optional method used to provide additional configurations to the client builder.</param>
    /// <param name="lifetime">The registered lifetime of the Dapr client.</param>
    /// <returns>The collection of DI-registered services.</returns>
    //internal static TBuilderInterface AddDaprClient<TBuilderInterface, TClient, TClientBuilder>(
    internal static TBuilderInterface AddDaprClient<TClient, TBuilderInterface, TClientBuilder>(
        this IServiceCollection services,
        Action<IServiceProvider, TClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TBuilderInterface : class, IDaprServiceBuilder
        where TClient : class, IDaprClient
        where TClientBuilder : DaprGenericClientBuilder<TClient>, new()
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.AddHttpClient();

        var registration = new Func<IServiceProvider, TClient>(provider =>
        {
            var configuration = provider.GetService<IConfiguration>();
            var builder = (TClientBuilder)Activator.CreateInstance(typeof(TClientBuilder), configuration)!;
        
             builder.UseDaprApiToken(DaprDefaults.GetDefaultDaprApiToken(configuration));
             configure?.Invoke(provider, builder);
             var (channel, httpClient, httpEndpoint, daprApiToken) =
                 builder.BuildDaprClientDependencies(Assembly.GetExecutingAssembly());
             return (TClient)Activator.CreateInstance(typeof(TClient), channel, httpClient, httpEndpoint, daprApiToken)!;
        });
        
        services.Add(new ServiceDescriptor(typeof(TClient), registration, lifetime));

        return (TBuilderInterface)Activator.CreateInstance(typeof(TBuilderInterface), services)!;
    }
}
