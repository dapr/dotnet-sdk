// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.BinaryStore.Extensions;

/// <summary>
/// Contains extension methods for registering Dapr Binary Store services with dependency injection.
/// </summary>
public static class DaprBinaryStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds a <see cref="DaprBinaryStoreClient"/> to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <param name="configure">
    /// An optional callback to further configure the <see cref="DaprBinaryStoreClientBuilder"/>
    /// using services resolved from the container.
    /// </param>
    /// <param name="lifetime">
    /// The <see cref="ServiceLifetime"/> of the registered client. Defaults to
    /// <see cref="ServiceLifetime.Singleton"/>, which is recommended for most scenarios.
    /// </param>
    /// <returns>
    /// An <see cref="IDaprBinaryStoreBuilder"/> that can be used for further configuration.
    /// </returns>
    public static IDaprBinaryStoreBuilder AddDaprBinaryStoreClient(
        this IServiceCollection services,
        Action<IServiceProvider, DaprBinaryStoreClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services.AddDaprClient<DaprBinaryStoreClient, DaprBinaryStoreGrpcClient,
            DaprBinaryStoreBuilder, DaprBinaryStoreClientBuilder>(
            config => new DaprBinaryStoreClientBuilder(config),
            svc => new DaprBinaryStoreBuilder(svc),
            configure,
            lifetime);
}
