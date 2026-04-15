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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Common.DependencyInjection;

/// <summary>
/// A fluent builder for configuring Dapr client services via dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// This builder provides a DI-first approach to configuring Dapr clients, replacing the
/// legacy static builder pattern. All configuration is registered in the
/// <see cref="IServiceCollection"/> and resolved at runtime through the DI container.
/// </para>
/// <para>
/// Specific Dapr building blocks (Actors, Workflows, etc.) extend this builder with
/// their own methods for registering building-block-specific services.
/// </para>
/// </remarks>
/// <typeparam name="TOptions">
/// The options type for the specific Dapr client, which must implement <see cref="IDaprClientOptions"/>
/// and have a parameterless constructor.
/// </typeparam>
public class DaprServiceBuilder<TOptions> : IDaprServiceBuilder
    where TOptions : class, IDaprClientOptions, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprServiceBuilder{TOptions}"/> class.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public DaprServiceBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// Configures a custom <see cref="IDaprSerializer"/> to replace the default JSON serializer.
    /// </summary>
    /// <param name="serializer">The custom serializer instance to use.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="serializer"/> is <see langword="null"/>.
    /// </exception>
    public DaprServiceBuilder<TOptions> WithSerializer(IDaprSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        Services.Replace(ServiceDescriptor.Singleton(serializer));
        return this;
    }

    /// <summary>
    /// Configures a custom <see cref="IDaprSerializer"/> using a factory method that can
    /// resolve dependencies from the DI container.
    /// </summary>
    /// <param name="serializerFactory">
    /// A factory function that creates the serializer using the service provider.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="serializerFactory"/> is <see langword="null"/>.
    /// </exception>
    public DaprServiceBuilder<TOptions> WithSerializer(Func<IServiceProvider, IDaprSerializer> serializerFactory)
    {
        ArgumentNullException.ThrowIfNull(serializerFactory);
        Services.Replace(ServiceDescriptor.Singleton(serializerFactory));
        return this;
    }
}
