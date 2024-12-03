// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Jobs.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Jobs with dependency injection.
/// </summary>
public static class DaprJobsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Jobs client support to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprJobsClient"/> using injected services.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprJobsClient(this IServiceCollection serviceCollection, Action<IServiceProvider, DaprJobsClientBuilder>? configure = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection, nameof(serviceCollection));

        //Register the IHttpClientFactory implementation
        serviceCollection.AddHttpClient();
        
        var registration = new Func<IServiceProvider, DaprJobsClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var configuration = serviceProvider.GetService<IConfiguration>();

            var builder = new DaprJobsClientBuilder(configuration);
            builder.UseHttpClientFactory(httpClientFactory);

            configure?.Invoke(serviceProvider, builder);

            return builder.Build();
        });
        
        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                serviceCollection.TryAddScoped(registration);
                break;
            case ServiceLifetime.Transient:
                serviceCollection.TryAddTransient(registration);
                break;
            case ServiceLifetime.Singleton:
            default:
                serviceCollection.TryAddSingleton(registration);
                break;
        }

        return serviceCollection;
    }
}
