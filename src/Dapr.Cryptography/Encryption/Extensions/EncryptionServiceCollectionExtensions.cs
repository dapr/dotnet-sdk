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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Cryptography.Encryption.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Encryption/Decryption capabilities with dependency injection.
/// </summary>
public static class EncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr encryption/decryption support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprEncryptionClient"/>.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprEncryptionClient(
        this IServiceCollection services,
        Action<IServiceProvider, DaprEncryptionClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        
        //Register the IHttpClientFactory implementation
        services.AddHttpClient();

        var registration = new Func<IServiceProvider, DaprEncryptionClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var configuration = serviceProvider.GetService<IConfiguration>();

            var builder = new DaprEncryptionClientBuilder(configuration);
            builder.UseHttpClientFactory(httpClientFactory);

            configure?.Invoke(serviceProvider, builder);

            return builder.Build();
        });

        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                services.TryAddScoped<IDaprEncryptionClient>(registration);
                // ReSharper disable once RedundantTypeArgumentsOfMethod
                services.TryAddScoped<DaprEncryptionClient>(registration);
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<IDaprEncryptionClient>(registration);
                // ReSharper disable once RedundantTypeArgumentsOfMethod
                services.TryAddTransient<DaprEncryptionClient>(registration);
                break;
            default:
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<IDaprEncryptionClient>(registration);
                // ReSharper disable once RedundantTypeArgumentsOfMethod
                services.TryAddSingleton<DaprEncryptionClient>(registration);
                break;
        }

        return services;
    }
}
