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

using System;
using System.Collections.ObjectModel;

namespace Dapr.Actors.Runtime;

/// <summary>
/// A collection of <see cref="ActorRegistration" /> instances.
/// </summary>
public sealed class ActorRegistrationCollection : KeyedCollection<ActorTypeInformation, ActorRegistration>
{
    /// <summary>
    /// Returns the key for the item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The key.</returns>
    protected override ActorTypeInformation GetKeyForItem(ActorRegistration item)
    {
        return item.Type;
    }

    /// <summary>
    /// Registers an actor type in the collection.
    /// </summary>
    /// <typeparam name="TActor">Type of actor.</typeparam>
    /// <param name="configure">An optional delegate used to configure the actor registration.</param>
    public void RegisterActor<TActor>(Action<ActorRegistration> configure = null)
        where TActor : Actor
    {
        RegisterActor<TActor>(actorTypeName: null, configure);
    }

    /// <summary>
    /// Registers an actor type in the collection.
    /// </summary>
    /// <typeparam name="TActor">Type of actor.</typeparam>
    /// <param name="typeOptions">An optional <see cref="ActorRuntimeOptions"/> that defines values for this type alone.</param>
    /// <param name="configure">An optional delegate used to configure the actor registration.</param>
    public void RegisterActor<TActor>(ActorRuntimeOptions typeOptions, Action<ActorRegistration> configure = null)
        where TActor : Actor
    {
        RegisterActor<TActor>(null, typeOptions, configure);
    }

    /// <summary>
    /// Registers an actor type in the collection.
    /// </summary>
    /// <typeparam name="TActor">Type of actor.</typeparam>
    /// <param name="actorTypeName">The name of the actor type represented by the actor.</param>
    /// <param name="configure">An optional delegate used to configure the actor registration.</param>
    /// <remarks>The value of <paramref name="actorTypeName"/> will have precedence over the default actor type name derived from the actor implementation type or any type name set via <see cref="ActorAttribute"/>.</remarks>
    public void RegisterActor<TActor>(string actorTypeName, Action<ActorRegistration> configure = null)
        where TActor : Actor
    {
        RegisterActor<TActor>(actorTypeName, null, configure);
    }

    /// <summary>
    /// Registers an actor type in the collection.
    /// </summary>
    /// <typeparam name="TActor">Type of actor.</typeparam>
    /// <param name="actorTypeName">The name of the actor type represented by the actor.</param>
    /// <param name="typeOptions">An optional <see cref="ActorRuntimeOptions"/> that defines values for this type alone.</param>
    /// <param name="configure">An optional delegate used to configure the actor registration.</param>
    /// <remarks>The value of <paramref name="actorTypeName"/> will have precedence over the default actor type name derived from the actor implementation type or any type name set via <see cref="ActorAttribute"/>.</remarks>
    public void RegisterActor<TActor>(string actorTypeName, ActorRuntimeOptions typeOptions, Action<ActorRegistration> configure = null)
        where TActor : Actor
    {
        var actorTypeInfo = ActorTypeInformation.Get(typeof(TActor), actorTypeName);
        var registration = new ActorRegistration(actorTypeInfo, typeOptions);
        configure?.Invoke(registration);
        this.Add(registration);
    }
}