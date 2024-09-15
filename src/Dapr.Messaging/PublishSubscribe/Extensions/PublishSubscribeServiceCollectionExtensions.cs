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

using System.Runtime.InteropServices.ComTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Messaging.PublishSubscribe.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Publish/Subscribe with dependency injection.
/// </summary>
public static class PublishSubscribeServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Publish/Subscribe support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services) =>
        AddDaprPubSubClient(services, (_, _) => { });

    /// <summary>
    /// Adds Dapr Publish/Subscribe support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprPublishSubscribeClientBuilder"/>.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services,
        Action<DaprPublishSubscribeClientBuilder>? configure) =>
        services.AddDaprPubSubClient((_, builder) => configure?.Invoke(builder));

    /// <summary>
    /// Adds Dapr Publish/Subscribe support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprPublishSubscribeClient"/> using injected services.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services, Action<IServiceProvider, DaprPublishSubscribeClientBuilder>? configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        //Register the IHttpClientFactory implementation
        services.AddHttpClient();

        services.TryAddSingleton(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            var builder = new DaprPublishSubscribeClientBuilder();
            builder.UseHttpClientFactory(httpClientFactory);

            configure?.Invoke(serviceProvider, builder);

            return builder.Build();
        });

        return services;
    }
}
