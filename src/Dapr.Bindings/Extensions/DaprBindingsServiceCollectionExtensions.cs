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

using Dapr.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Bindings.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Bindings with dependency injection.
/// </summary>
public static class DaprBindingsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Bindings client support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprBindingsClient"/> using injected services.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    /// <returns></returns>
    public static IDaprBindingsBuilder AddDaprBindingsClient(
        this IServiceCollection services,
        Action<IServiceProvider, DaprBindingsClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services
            .AddDaprClient<DaprBindingsClient, DaprBindingsGrpcClient, DaprBindingsBuilder, DaprBindingsClientBuilder>(
                configure, lifetime);
}
