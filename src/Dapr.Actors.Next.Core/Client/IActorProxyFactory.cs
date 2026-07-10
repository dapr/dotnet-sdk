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
/// Creates generated strongly typed actor proxies.
/// </summary>
/// <remarks>
/// This is the preferred API for application code. Resolve this service from dependency injection and use it
/// wherever a service, endpoint, or actor needs to call another actor.
/// </remarks>
public interface IActorProxyFactory
{
    /// <summary>
    /// Creates a proxy for an actor interface.
    /// </summary>
    TActor Create<TActor>(ActorId actorId, string actorType)
        where TActor : IActor;
}
