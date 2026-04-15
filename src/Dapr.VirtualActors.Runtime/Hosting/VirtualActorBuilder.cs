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

using Dapr.Common.DependencyInjection;
using Dapr.Common.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Fluent builder for configuring Dapr Virtual Actors services.
/// </summary>
/// <remarks>
/// <para>
/// Provides extension points for add-on projects to register middleware, lifecycle
/// observers, custom activators, state providers, and other services. Returned by
/// <see cref="VirtualActorServiceCollectionExtensions.AddDaprVirtualActors(IServiceCollection, Action{VirtualActorOptions})"/>.
/// </para>
/// <para>
/// Example of an add-on project extending the builder:
/// <code>
/// // In Dapr.AgenticFramework
/// public static class AgenticActorBuilderExtensions
/// {
///     public static VirtualActorBuilder UseAgenticFramework(this VirtualActorBuilder builder)
///     {
///         builder.UseMiddleware&lt;AgenticContextMiddleware&gt;();
///         builder.AddLifecycleObserver&lt;EmbeddingPreloadObserver&gt;();
///         return builder;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class VirtualActorBuilder : DaprServiceBuilder<VirtualActorOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public VirtualActorBuilder(IServiceCollection services) : base(services)
    {
    }

    /// <summary>
    /// Registers an <see cref="IActorMiddleware"/> component in the method invocation pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type. Must have a public constructor resolvable from DI.</typeparam>
    /// <returns>This builder for fluent chaining.</returns>
    /// <remarks>
    /// Middleware components are invoked in registration order for each actor method call.
    /// </remarks>
    public VirtualActorBuilder UseMiddleware<TMiddleware>() where TMiddleware : class, IActorMiddleware
    {
        Services.AddSingleton<IActorMiddleware, TMiddleware>();
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IActorMiddleware"/> instance in the method invocation pipeline.
    /// </summary>
    /// <param name="middleware">The middleware instance.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public VirtualActorBuilder UseMiddleware(IActorMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        Services.AddSingleton(middleware);
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IActorLifecycleObserver"/> to receive notifications
    /// about actor lifecycle events.
    /// </summary>
    /// <typeparam name="TObserver">The observer type.</typeparam>
    /// <returns>This builder for fluent chaining.</returns>
    public VirtualActorBuilder AddLifecycleObserver<TObserver>() where TObserver : class, IActorLifecycleObserver
    {
        Services.AddSingleton<IActorLifecycleObserver, TObserver>();
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IActorLifecycleObserver"/> instance.
    /// </summary>
    /// <param name="observer">The observer instance.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public VirtualActorBuilder AddLifecycleObserver(IActorLifecycleObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        Services.AddSingleton(observer);
        return this;
    }

    /// <summary>
    /// Replaces the default <see cref="IActorActivator"/> with a custom implementation.
    /// </summary>
    /// <typeparam name="TActivator">The custom activator type.</typeparam>
    /// <returns>This builder for fluent chaining.</returns>
    public VirtualActorBuilder UseActorActivator<TActivator>() where TActivator : class, IActorActivator
    {
        Services.Replace(ServiceDescriptor.Singleton<IActorActivator, TActivator>());
        return this;
    }

    /// <summary>
    /// Replaces the default <see cref="IActorTimerManager"/> with a custom implementation.
    /// </summary>
    /// <typeparam name="TManager">The custom timer manager type.</typeparam>
    /// <returns>This builder for fluent chaining.</returns>
    public VirtualActorBuilder UseTimerManager<TManager>() where TManager : class, IActorTimerManager
    {
        Services.Replace(ServiceDescriptor.Singleton<IActorTimerManager, TManager>());
        return this;
    }

    /// <summary>
    /// Replaces the default <see cref="IVirtualActorProxyFactory"/> with a custom implementation.
    /// </summary>
    /// <typeparam name="TFactory">The custom proxy factory type.</typeparam>
    /// <returns>This builder for fluent chaining.</returns>
    public VirtualActorBuilder UseProxyFactory<TFactory>() where TFactory : class, IVirtualActorProxyFactory
    {
        Services.Replace(ServiceDescriptor.Singleton<IVirtualActorProxyFactory, TFactory>());
        return this;
    }
}
