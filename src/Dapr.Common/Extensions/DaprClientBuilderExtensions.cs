// ------------------------------------------------------------------------
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
    /// <typeparam name="TClient">The abstract Dapr client type being created.</typeparam>
    /// <typeparam name="TConcreteClient">The concrete Dapr client type being created.</typeparam>
    /// <typeparam name="TServiceBuilder">The type of the DI-builder wrapper returned to the caller.</typeparam>
    /// <typeparam name="TClientBuilder">The strongly-typed builder used to configure and construct the Dapr client.</typeparam>
    /// <param name="services">The collection of services to which the Dapr client and associated services are being registered.</param>
    /// <param name="clientBuilderFactory">
    /// A factory that creates a <typeparamref name="TClientBuilder"/> from an optional <see cref="IConfiguration"/>.
    /// Typically <c>config => new TClientBuilder(config)</c>.
    /// </param>
    /// <param name="serviceBuilderFactory">
    /// A factory that creates a <typeparamref name="TServiceBuilder"/> from the <see cref="IServiceCollection"/>.
    /// Typically <c>svc => new TServiceBuilder(svc)</c>.
    /// </param>
    /// <param name="configure">An optional method used to provide additional configurations to the client builder.</param>
    /// <param name="lifetime">The registered lifetime of the Dapr client.</param>
    /// <returns>The <typeparamref name="TServiceBuilder"/> that wraps the service collection for further configuration.</returns>
    internal static TServiceBuilder AddDaprClient<TClient, TConcreteClient, TServiceBuilder, TClientBuilder>(
        this IServiceCollection services,
        Func<IConfiguration?, TClientBuilder> clientBuilderFactory,
        Func<IServiceCollection, TServiceBuilder> serviceBuilderFactory,
        Action<IServiceProvider, TClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TClient : class, IDaprClient
        where TConcreteClient : TClient
        where TServiceBuilder : class, IDaprServiceBuilder
        where TClientBuilder : DaprGenericClientBuilder<TClient>
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(clientBuilderFactory, nameof(clientBuilderFactory));
        ArgumentNullException.ThrowIfNull(serviceBuilderFactory, nameof(serviceBuilderFactory));

        // Ensure that TConcreteClient is a concrete class.
        if (typeof(TConcreteClient).IsInterface || typeof(TConcreteClient).IsAbstract)
        {
            throw new ArgumentException($"{typeof(TConcreteClient).Name} must be a concrete class",
                nameof(TConcreteClient));
        }

        // Ensure that TServiceBuilder is a concrete class.
        if (typeof(TServiceBuilder).IsInterface || typeof(TServiceBuilder).IsAbstract)
        {
            throw new ArgumentException($"{typeof(TServiceBuilder).Name} must be a concrete class",
                nameof(TServiceBuilder));
        }

        services.AddHttpClient();
        
        // Register the TClient type for use by the SDKs
        services.Add(new ServiceDescriptor(typeof(TClient), provider =>
        {
            var configuration = provider.GetService<IConfiguration>();

            var builder = clientBuilderFactory(configuration);

            builder.UseDaprApiToken(DaprDefaults.GetDefaultDaprApiToken(configuration));
            configure?.Invoke(provider, builder);

            // Delegate to the builder's Build() method so each client can supply its own
            // construction logic (e.g., passing JsonSerializerOptions). This also ensures
            // the correct assembly is used for the User-Agent header rather than Dapr.Common.
            return builder.Build();
        });

        services.Add(new ServiceDescriptor(typeof(TClient), registration, lifetime));

        return serviceBuilderFactory(services);
    }
}
