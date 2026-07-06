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
using System.Runtime.Serialization;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

/// <summary>
/// Encapsulation of a reference to an actor for serialization.
/// </summary>
[DataContract(Name = "ActorReference", Namespace = Constants.Namespace)]
[Serializable]
public sealed class ActorReference : IActorReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorReference"/> class.
    /// </summary>
    public ActorReference()
    {
    }

    /// <summary>
    /// Gets or sets the <see cref="Dapr.Actors.ActorId"/> of the actor.
    /// </summary>
    /// <value><see cref="Dapr.Actors.ActorId"/> of the actor.</value>
    [DataMember(Name = "ActorId", Order = 0, IsRequired = true)]
    public ActorId ActorId { get; set; }

    /// <summary>
    /// Gets or sets the implementation type of the actor.
    /// </summary>
    /// <value>Implementation type name of the actor.</value>
    [DataMember(Name = "ActorType", Order = 0, IsRequired = true)]
    public string ActorType { get; set; }

    /// <summary>
    /// Gets <see cref="ActorReference"/> for the actor.
    /// </summary>
    /// <param name="actor">Actor object to get <see cref="ActorReference"/> for.</param>
    /// <returns><see cref="ActorReference"/> object for the actor.</returns>
    /// <remarks>A null value is returned if actor is passed as null.</remarks>
    public static ActorReference Get(object actor)
    {
        if (actor != null)
        {
            return GetActorReference(actor);
        }

        return null;
    }

    /// <inheritdoc/>
    public object Bind(Type actorInterfaceType)
    {
        return ActorProxy.DefaultProxyFactory.CreateActorProxy(this.ActorId, actorInterfaceType, this.ActorType);
    }

    private static ActorReference GetActorReference(object actor)
    {
        ArgumentNullException.ThrowIfNull(actor, nameof(actor));

        var actorReference = actor switch
        {
            // try as IActorProxy for backward compatibility as customers's mock framework may rely on it before V2 remoting stack.
            IActorProxy actorProxy => new ActorReference()
            {
                ActorId = actorProxy.ActorId,
                ActorType = actorProxy.ActorType,
            },
            // Handle case when we want to get ActorReference inside the Actor implementation,
            // we gather actor id and actor type from Actor base class.
            Actor actorBase => new ActorReference()
            {
                ActorId = actorBase.Id,
                ActorType = actorBase.Host.ActorTypeInfo.ActorTypeName,
            },
            // Handle case when we can't cast to IActorProxy or Actor.
            _ => throw new ArgumentOutOfRangeException("actor", "Invalid actor object type."),
        };

        return actorReference;
    }
}