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

using Dapr.VirtualActors.Runtime.State;
using Microsoft.Extensions.Logging;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Factory for creating <see cref="VirtualActorHost"/> instances for specific actors.
/// </summary>
/// <remarks>
/// Registered as a scoped service so each actor activation gets its own host
/// with the correct state manager, timer manager, and proxy factory references.
/// </remarks>
internal sealed class VirtualActorHostFactory
{
    private readonly IActorStateProvider _stateProvider;
    private readonly IActorTimerManager _timerManager;
    private readonly IVirtualActorProxyFactory _proxyFactory;
    private readonly ILoggerFactory _loggerFactory;

    public VirtualActorHostFactory(
        IActorStateProvider stateProvider,
        IActorTimerManager timerManager,
        IVirtualActorProxyFactory proxyFactory,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(stateProvider);
        ArgumentNullException.ThrowIfNull(timerManager);
        ArgumentNullException.ThrowIfNull(proxyFactory);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _stateProvider = stateProvider;
        _timerManager = timerManager;
        _proxyFactory = proxyFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new <see cref="VirtualActorHost"/> for the specified actor.
    /// </summary>
    /// <param name="actorType">The actor type metadata.</param>
    /// <param name="actorId">The actor identity.</param>
    /// <returns>A fully configured <see cref="VirtualActorHost"/>.</returns>
    public VirtualActorHost Create(ActorTypeInformation actorType, VirtualActorId actorId)
    {
        ArgumentNullException.ThrowIfNull(actorType);

        var stateManager = new VirtualActorStateManager(
            actorType.ActorTypeName,
            actorId,
            _stateProvider);

        return new VirtualActorHost(
            actorId,
            actorType.ActorTypeName,
            stateManager,
            _proxyFactory,
            _timerManager,
            _loggerFactory);
    }
}
