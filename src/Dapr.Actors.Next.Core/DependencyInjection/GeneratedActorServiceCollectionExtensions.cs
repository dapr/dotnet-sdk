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

using System.ComponentModel;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Core.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Actors.Next.Core.DependencyInjection;

/// <summary>
/// Composes the proxy factories and metadata contributed by generated actor modules.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedActorServiceCollectionExtensions
{
    /// <summary>
    /// Adds one generated module without replacing contributions from other assemblies.
    /// </summary>
    public static IServiceCollection AddGeneratedActorModule(
        this IServiceCollection services,
        IReadOnlyList<Type> interfaces,
        Func<IServiceProvider, IActorProxyFactory> createFactory,
        IActorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(createFactory);
        ArgumentNullException.ThrowIfNull(registry);
        services.AddSingleton(new GeneratedActorModule(interfaces.ToArray(), createFactory, registry));
        services.TryAddSingleton<IActorProxyFactory>(sp =>
            new CompositeActorProxyFactory(sp, sp.GetServices<GeneratedActorModule>()));
        services.TryAddSingleton<IActorRegistry>(sp =>
            new CompositeActorRegistry(sp.GetServices<GeneratedActorModule>()));
        return services;
    }

    private sealed record GeneratedActorModule(
        IReadOnlyList<Type> Interfaces,
        Func<IServiceProvider, IActorProxyFactory> CreateFactory,
        IActorRegistry Registry);

    private sealed class CompositeActorProxyFactory : IActorProxyFactory
    {
        private readonly Dictionary<Type, IActorProxyFactory> factories = [];

        public CompositeActorProxyFactory(IServiceProvider services, IEnumerable<GeneratedActorModule> modules)
        {
            foreach (var module in modules)
            {
                var factory = module.CreateFactory(services);
                foreach (var actorInterface in module.Interfaces)
                {
                    factories.TryAdd(actorInterface, factory);
                }
            }
        }

        public TActor Create<TActor>(ActorId actorId, string actorType) where TActor : IActor =>
            factories.TryGetValue(typeof(TActor), out var factory)
                ? factory.Create<TActor>(actorId, actorType)
                : throw new InvalidOperationException(
                    $"Actor interface '{typeof(TActor).FullName}' is not generated in any registered assembly.");
    }

    private sealed class CompositeActorRegistry : IActorRegistry
    {
        private readonly Dictionary<string, ActorTypeDescriptor> byName;

        public CompositeActorRegistry(IEnumerable<GeneratedActorModule> modules)
        {
            Actors = modules.SelectMany(module => module.Registry.Actors)
                .OrderBy(actor => actor.ActorType, StringComparer.Ordinal).ToArray();
            byName = Actors.ToDictionary(actor => actor.ActorType, StringComparer.Ordinal);
        }

        public IReadOnlyList<ActorTypeDescriptor> Actors { get; }

        public bool TryGet(string actorType, out ActorTypeDescriptor descriptor) =>
            byName.TryGetValue(actorType, out descriptor!);
    }
}
