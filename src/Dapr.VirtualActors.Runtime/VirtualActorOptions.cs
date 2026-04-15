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
    /// Registers an actor type with the runtime.
    /// </summary>
    /// <typeparam name="TActor">The actor implementation type.</typeparam>
    /// <param name="actorTypeName">
    /// Optional custom actor type name. If not specified, the class name is used.
    /// </param>
    public void RegisterActor<TActor>(string? actorTypeName = null) where TActor : VirtualActor
    {
        var implType = typeof(TActor);
        var name = actorTypeName ?? implType.Name;
        var interfaces = implType.GetInterfaces()
            .Where(i => typeof(IVirtualActor).IsAssignableFrom(i) && i != typeof(IVirtualActor))
            .ToList();

        var typeInfo = new ActorTypeInformation(name, implType, interfaces);
        _actorRegistrations.Add(new ActorRegistration(typeInfo));
    }
}
