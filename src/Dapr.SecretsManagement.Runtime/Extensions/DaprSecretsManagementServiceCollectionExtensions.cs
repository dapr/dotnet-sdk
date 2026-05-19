// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
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

using Dapr.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.SecretsManagement.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Secrets Management with dependency injection.
/// </summary>
public static class DaprSecretsManagementServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Secrets Management client support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">
    /// An optional callback used to configure the <see cref="DaprSecretsManagementClientBuilder"/> with injected services.
    /// </param>
    /// <param name="lifetime">The lifetime of the registered services. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>An <see cref="IDaprSecretsManagementBuilder"/> that can be used for further configuration.</returns>
    public static IDaprSecretsManagementBuilder AddDaprSecretsManagementClient(
        this IServiceCollection services,
        Action<IServiceProvider, DaprSecretsManagementClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services.AddDaprClient<DaprSecretsManagementClient, DaprSecretsManagementGrpcClient, DaprSecretsManagementBuilder, DaprSecretsManagementClientBuilder>(
            configure, lifetime);
}
