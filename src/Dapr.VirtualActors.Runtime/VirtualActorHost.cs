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

using Microsoft.Extensions.Logging;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Provides runtime services to an actor instance during its lifetime.
/// </summary>
/// <remarks>
/// The host is created by the runtime for each actor activation and provides
/// access to the actor's identity, state manager, proxy factory, and logging.
/// It is passed to the actor constructor via dependency injection.
/// </remarks>
public sealed class VirtualActorHost
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorHost"/> class.
    /// </summary>
    /// <param name="id">The identity of the actor.</param>
    /// <param name="actorType">The type name of the actor.</param>
    /// <param name="stateManager">The state manager for the actor.</param>
    /// <param name="proxyFactory">The proxy factory for inter-actor communication.</param>
    /// <param name="timerManager">The timer and reminder manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public VirtualActorHost(
        VirtualActorId id,
        string actorType,
        IActorStateManager stateManager,
        IVirtualActorProxyFactory proxyFactory,
        IActorTimerManager timerManager,
        ILoggerFactory loggerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(timerManager);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Id = id;
        ActorType = actorType;
        StateManager = stateManager;
        ProxyFactory = proxyFactory;
        TimerManager = timerManager;
        LoggerFactory = loggerFactory;
    }

    /// <summary>
    /// Gets the identity of the actor.
    /// </summary>
    public VirtualActorId Id { get; }

    /// <summary>
    /// Gets the actor type name.
    /// </summary>
    public string ActorType { get; }

    /// <summary>
    /// Gets the state manager for the actor.
    /// </summary>
    public IActorStateManager StateManager { get; }

    /// <summary>
    /// Gets the proxy factory for inter-actor communication.
    /// </summary>
    public IVirtualActorProxyFactory ProxyFactory { get; }

    /// <summary>
    /// Gets the timer and reminder manager.
    /// </summary>
    public IActorTimerManager TimerManager { get; }

    /// <summary>
    /// Gets the logger factory.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Creates a <see cref="VirtualActorHost"/> for unit testing purposes.
    /// </summary>
    /// <typeparam name="TActor">The actor type to create a host for.</typeparam>
    /// <param name="actorId">Optional actor ID. Defaults to a random value.</param>
    /// <param name="stateManager">Optional state manager mock.</param>
    /// <param name="proxyFactory">Optional proxy factory mock.</param>
    /// <param name="timerManager">Optional timer/reminder manager mock.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>A <see cref="VirtualActorHost"/> suitable for unit tests.</returns>
    public static VirtualActorHost CreateForTest<TActor>(
        VirtualActorId? actorId = null,
        IActorStateManager? stateManager = null,
        IVirtualActorProxyFactory? proxyFactory = null,
        IActorTimerManager? timerManager = null,
        ILoggerFactory? loggerFactory = null) where TActor : VirtualActor
    {
        return new VirtualActorHost(
            actorId ?? new VirtualActorId(Guid.NewGuid().ToString()),
            typeof(TActor).Name,
            stateManager ?? new NoOpActorStateManager(),
            proxyFactory ?? new NoOpVirtualActorProxyFactory(),
            timerManager ?? new NoOpActorTimerManager(),
            loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
    }
}
