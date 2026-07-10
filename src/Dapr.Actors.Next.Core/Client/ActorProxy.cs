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

using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Convenience entry point for generated strongly typed actor proxies.
/// </summary>
/// <remarks>
/// Prefer injecting <see cref="IActorProxyFactory"/> and calling <see cref="IActorProxyFactory.Create{TActor}(ActorId, string)"/>
/// in application code. This type stores a process-wide factory and is intended for controlled scenarios such as
/// tests, samples, or hosts that explicitly configure the generated factory before creating proxies.
/// </remarks>
public static class ActorProxy
{
    private static IActorProxyFactory? _factory;

    /// <summary>
    /// Configures the process-wide generated proxy factory used by <see cref="Create{TActor}(ActorId, string)"/>.
    /// </summary>
    /// <remarks>
    /// Prefer resolving <see cref="IActorProxyFactory"/> from dependency injection instead of configuring this
    /// static facade in application code. Reconfiguring this value affects all subsequent static proxy creation
    /// in the current process.
    /// </remarks>
    public static void Configure(IActorProxyFactory proxyFactory)
    {
        _factory = proxyFactory ?? throw new ArgumentNullException(nameof(proxyFactory));
    }

    /// <summary>
    /// Clears the process-wide generated proxy factory used by <see cref="Create{TActor}(ActorId, string)"/>.
    /// </summary>
    /// <remarks>
    /// This is primarily intended for test cleanup after using <see cref="Configure(IActorProxyFactory)"/>.
    /// </remarks>
    public static void Reset()
    {
        _factory = null;
    }

    /// <summary>
    /// Creates a strongly typed actor proxy using the configured process-wide factory.
    /// </summary>
    /// <remarks>
    /// Prefer injecting <see cref="IActorProxyFactory"/> and calling <see cref="IActorProxyFactory.Create{TActor}(ActorId, string)"/>
    /// in application code. This method depends on prior process-wide configuration via
    /// <see cref="Configure(IActorProxyFactory)"/>.
    /// </remarks>
    public static TActor Create<TActor>(ActorId actorId, string actorType)
        where TActor : IActor
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("ActorProxy is not configured. The source generator must provide an IActorProxyFactory.");
        }

        return _factory.Create<TActor>(actorId, actorType);
    }
}
