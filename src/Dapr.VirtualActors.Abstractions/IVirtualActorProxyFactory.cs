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

namespace Dapr.VirtualActors;

/// <summary>
/// Factory interface for creating actor proxy instances through the DI container.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the legacy <c>ActorProxy.Create</c> static API, this factory is registered
/// in the DI container and injected where needed. It uses gRPC for all actor method
/// invocations and leverages the configured <see cref="Dapr.Common.Serialization.IDaprSerializer"/>
/// for serialization.
/// </para>
/// <para>
/// Resolve this from the DI container:
/// <code>
/// public class MyService(IVirtualActorProxyFactory proxyFactory) { ... }
/// </code>
/// </para>
/// </remarks>
public interface IVirtualActorProxyFactory
{
    /// <summary>
    /// Creates a strongly-typed proxy to a virtual actor.
    /// </summary>
    /// <typeparam name="TActorInterface">
    /// The actor interface implemented by the remote actor. Must derive from <see cref="IVirtualActor"/>.
    /// </typeparam>
    /// <param name="actorId">The unique ID of the actor to create a proxy for.</param>
    /// <param name="actorType">
    /// The actor type name as registered in Dapr. If <see langword="null"/>,
    /// the name is inferred from the interface type.
    /// </param>
    /// <returns>A proxy object implementing <typeparamref name="TActorInterface"/>.</returns>
    TActorInterface CreateProxy<TActorInterface>(VirtualActorId actorId, string? actorType = null)
        where TActorInterface : IVirtualActor;

    /// <summary>
    /// Creates a weakly-typed proxy to a virtual actor for dynamic method invocation.
    /// </summary>
    /// <param name="actorId">The unique ID of the actor to create a proxy for.</param>
    /// <param name="actorType">The actor type name as registered in Dapr.</param>
    /// <returns>A proxy object for invoking actor methods dynamically.</returns>
    IVirtualActorProxy CreateProxy(VirtualActorId actorId, string actorType);
}
