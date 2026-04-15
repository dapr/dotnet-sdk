// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.Common.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dapr.Common.DependencyInjection;

/// <summary>
/// Extension methods for registering core Dapr services with the dependency injection container.
/// </summary>
public static class DaprServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core Dapr services that are shared across all building blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is called internally by building-block-specific registration methods
    /// (e.g., <c>AddDaprVirtualActors</c>, <c>AddDaprWorkflow</c>) to ensure common
    /// infrastructure is available. It is idempotent and safe to call multiple times.
    /// </para>
    /// <para>
    /// Registers the following services:
    /// <list type="bullet">
    /// <item><see cref="IDaprSerializer"/> — defaults to <see cref="JsonDaprSerializer"/></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to register services with.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDaprCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDaprSerializer>(new JsonDaprSerializer());

        return services;
    }

    /// <summary>
    /// Populates default values on an <see cref="IDaprClientOptions"/> instance from
    /// environment variables and <see cref="IConfiguration"/> if the values are not
    /// already set.
    /// </summary>
    /// <typeparam name="TOptions">The options type to populate.</typeparam>
    /// <param name="options">The options instance to populate.</param>
    /// <param name="configuration">
    /// Optional <see cref="IConfiguration"/> instance for resolving values.
    /// </param>
    public static void PopulateDefaults<TOptions>(TOptions options, IConfiguration? configuration = null)
        where TOptions : class, IDaprClientOptions
    {
        ArgumentNullException.ThrowIfNull(options);

        options.GrpcEndpoint ??= DaprDefaults.GetDefaultGrpcEndpoint(configuration);
        options.HttpEndpoint ??= DaprDefaults.GetDefaultHttpEndpoint(configuration);
        options.DaprApiToken ??= DaprDefaults.GetDefaultDaprApiToken(configuration);
    }
}
