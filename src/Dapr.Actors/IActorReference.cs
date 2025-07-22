// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors;

using System;
using Dapr.Actors.Client;

/// <summary>
/// Interface for ActorReference.
/// </summary>
internal interface IActorReference
{
    /// <summary>
    /// Creates an <see cref="ActorProxy"/> that implements an actor interface for the actor using the
    ///     <see cref="ActorProxyFactory.CreateActorProxy(Dapr.Actors.ActorId, System.Type, string, ActorProxyOptions)"/>
    /// method.
    /// </summary>
    /// <param name="actorInterfaceType">Actor interface for the created <see cref="ActorProxy"/> to implement.</param>
    /// <returns>An actor proxy object that implements <see cref="IActorProxy"/> and TActorInterface.</returns>
    object Bind(Type actorInterfaceType);
}