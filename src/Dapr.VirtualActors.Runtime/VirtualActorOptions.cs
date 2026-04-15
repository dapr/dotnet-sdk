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
using Dapr.VirtualActors;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Options for configuring the Dapr Virtual Actors runtime.
/// </summary>
/// <remarks>
/// <para>
/// Used with <c>Microsoft.Extensions.Options</c> to configure actor registration,
/// timeouts, reentrancy, and other runtime behaviors via DI.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// services.AddDaprVirtualActors(options =>
/// {
///     options.RegisterActor&lt;MyActor&gt;();
///     options.Reentrancy.Enabled = true;
///     options.ActorIdleTimeout = TimeSpan.FromMinutes(10);
/// });
/// </code>
/// </para>
/// </remarks>
public sealed class VirtualActorOptions : DaprClientOptions
{
    private readonly List<ActorRegistration> _actorRegistrations = [];

    /// <summary>
    /// Gets the list of registered actor types.
    /// </summary>
    public IReadOnlyList<ActorRegistration> ActorRegistrations => _actorRegistrations;

    /// <summary>
    /// Gets or sets the reentrancy configuration for actors.
    /// </summary>
    public ActorReentrancyOptions Reentrancy { get; set; } = new();

    /// <summary>
    /// Gets or sets the timeout before an idle actor is deactivated.
    /// </summary>
    /// <remarks>
    /// An actor is idle if no actor method calls or reminders have fired on it.
    /// <see langword="null"/> means the Dapr default is used.
    /// </remarks>
    public TimeSpan? ActorIdleTimeout { get; set; }

    /// <summary>
    /// Gets or sets the interval at which the runtime scans for idle actors to deactivate.
    /// </summary>
    public TimeSpan? ActorScanInterval { get; set; }

    /// <summary>
    /// Gets or sets how long to wait for ongoing calls to complete when draining rebalanced actors.
    /// </summary>
    public TimeSpan? DrainOngoingCallTimeout { get; set; }

    /// <summary>
    /// Gets or sets whether to wait for draining ongoing calls when rebalancing actors.
    /// </summary>
    public bool DrainRebalancedActors { get; set; }

    /// <summary>
    /// Gets or sets the number of partitions for reminder storage.
    /// </summary>
    public int? RemindersStoragePartitions { get; set; }

    /// <summary>
    /// Registers an actor type with an explicit factory delegate (AOT-safe, no reflection).
    /// </summary>
    /// <remarks>
    /// <para>
    /// In most cases, you do not need to call this method directly. The
    /// <c>Dapr.VirtualActors.Generators</c> source generator automatically discovers
    /// all <see cref="VirtualActor"/> subclasses in your project and generates
    /// registration code at compile time.
    /// </para>
    /// <para>
    /// Use this method only for advanced scenarios where you need to control
    /// actor construction explicitly (e.g., wrapping in a decorator).
    /// </para>
    /// </remarks>
    /// <param name="actorTypeName">The actor type name as known to Dapr.</param>
    /// <param name="interfaceTypes">The actor interfaces implemented by this type.</param>
    /// <param name="implementationType">The CLR type of the actor implementation.</param>
    /// <param name="factory">
    /// An AOT-safe factory delegate that creates actor instances. Receives the
    /// <see cref="VirtualActorHost"/> and <see cref="IServiceProvider"/> and returns
    /// a <see cref="VirtualActor"/> instance.
    /// </param>
    public void RegisterActor(
        string actorTypeName,
        IReadOnlyList<Type> interfaceTypes,
        Type implementationType,
        Func<VirtualActorHost, IServiceProvider, VirtualActor> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorTypeName);
        ArgumentNullException.ThrowIfNull(interfaceTypes);
        ArgumentNullException.ThrowIfNull(implementationType);
        ArgumentNullException.ThrowIfNull(factory);

        var typeInfo = new ActorTypeInformation(actorTypeName, implementationType, interfaceTypes);
        _actorRegistrations.Add(new ActorRegistration(typeInfo, factory));
    }

    /// <summary>
    /// Registers an actor type using a strongly-typed factory delegate (AOT-safe, no reflection).
    /// </summary>
    /// <typeparam name="TActor">The actor implementation type.</typeparam>
    /// <param name="factory">
    /// A factory delegate that creates actor instances given a <see cref="VirtualActorHost"/>.
    /// </param>
    /// <param name="actorTypeName">
    /// Optional custom actor type name. If not specified, the class name is used.
    /// </param>
    public void RegisterActor<TActor>(
        Func<VirtualActorHost, TActor> factory,
        string? actorTypeName = null)
        where TActor : VirtualActor
    {
        ArgumentNullException.ThrowIfNull(factory);

        var name = actorTypeName ?? typeof(TActor).Name;

        RegisterActor(
            name,
            ActorInterfaceCache<TActor>.InterfaceTypes,
            typeof(TActor),
            (host, _) => factory(host));
    }

    /// <summary>
    /// Registers an actor type using a factory delegate that receives DI services (AOT-safe, no reflection).
    /// </summary>
    /// <typeparam name="TActor">The actor implementation type.</typeparam>
    /// <param name="factory">
    /// A factory delegate that creates actor instances given a <see cref="VirtualActorHost"/>
    /// and an <see cref="IServiceProvider"/> for resolving additional dependencies.
    /// </param>
    /// <param name="actorTypeName">
    /// Optional custom actor type name. If not specified, the class name is used.
    /// </param>
    public void RegisterActor<TActor>(
        Func<VirtualActorHost, IServiceProvider, TActor> factory,
        string? actorTypeName = null)
        where TActor : VirtualActor
    {
        ArgumentNullException.ThrowIfNull(factory);

        var name = actorTypeName ?? typeof(TActor).Name;

        RegisterActor(
            name,
            ActorInterfaceCache<TActor>.InterfaceTypes,
            typeof(TActor),
            (host, sp) => factory(host, sp));
    }
}

/// <summary>
/// AOT-safe cache of actor interface types computed once per generic instantiation.
/// </summary>
/// <typeparam name="TActor">The actor implementation type.</typeparam>
/// <remarks>
/// Uses a static generic class so the interface list is computed once at JIT/AOT time
/// per concrete actor type, with no runtime reflection overhead on subsequent accesses.
/// The initial <c>typeof(TActor)</c> metadata access is AOT-safe as it uses compile-time
/// type information only.
/// </remarks>
internal static class ActorInterfaceCache<TActor> where TActor : VirtualActor
{
    /// <summary>
    /// The <see cref="IVirtualActor"/> interfaces implemented by <typeparamref name="TActor"/>.
    /// </summary>
    public static readonly IReadOnlyList<Type> InterfaceTypes = GetActorInterfaces();

    private static IReadOnlyList<Type> GetActorInterfaces()
    {
        // Note: typeof(TActor).GetInterfaces() is AOT-safe because the generic type
        // parameter is known at compile time. The .NET runtime preserves interface
        // metadata for all types even in AOT scenarios.
        var interfaces = new List<Type>();
        foreach (var iface in typeof(TActor).GetInterfaces())
        {
            if (typeof(IVirtualActor).IsAssignableFrom(iface) && iface != typeof(IVirtualActor))
            {
                interfaces.Add(iface);
            }
        }
        return interfaces;
    }
}
