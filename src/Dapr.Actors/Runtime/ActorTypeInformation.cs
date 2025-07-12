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

namespace Dapr.Actors.Runtime;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Dapr.Actors.Resources;

/// <summary>
/// Contains the information about the type implementing an actor.
/// </summary>
public sealed class ActorTypeInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorTypeInformation"/> class.
    /// </summary>
    private ActorTypeInformation()
    {
    }

    /// <summary>
    /// Gets the name of the actor type represented by the actor.
    /// </summary>
    /// <value>The <see cref="string"/> name of the actor type represented by the actor.</value>
    /// <remarks>Defaults to the name of the class implementing the actor. Can be overridden using the <see cref="Dapr.Actors.Runtime.ActorAttribute.TypeName" /> property.</remarks>
    public string ActorTypeName { get; private set; }

    /// <summary>
    /// Gets the type of the class implementing the actor.
    /// </summary>
    /// <value>The <see cref="System.Type"/> of the class implementing the actor.</value>
    public Type ImplementationType { get; private set; }

    /// <summary>
    /// Gets the actor interface types which derive from <see cref="IActor"/> and implemented by actor class.
    /// </summary>
    /// <value>An enumerator that can be used to iterate through the actor interface type.</value>
    public IEnumerable<Type> InterfaceTypes { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the class implementing actor is abstract.
    /// </summary>
    /// <value>true if the class implementing actor is abstract, otherwise false.</value>
    public bool IsAbstract { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the actor class implements <see cref="IRemindable"/>.
    /// </summary>
    /// <value>true if the actor class implements <see cref="IRemindable"/>, otherwise false.</value>
    public bool IsRemindable { get; private set; }

    /// <summary>
    /// Creates the <see cref="ActorTypeInformation"/> from actorType.
    /// </summary>
    /// <param name="actorType">The type of class implementing the actor to create ActorTypeInformation for.</param>
    /// <param name="actorTypeInformation">When this method returns, contains ActorTypeInformation, if the creation of
    /// ActorTypeInformation from actorType succeeded, or null if the creation failed.
    /// The creation fails if the actorType parameter is null or it does not implement an actor.</param>
    /// <returns>true if ActorTypeInformation was successfully created for actorType; otherwise, false.</returns>
    /// <remarks>
    /// <para>Creation of ActorTypeInformation from actorType will fail when:</para>
    /// <para>1. <see cref="System.Type.BaseType"/> for actorType is not of type <see cref="Actor"/>.</para>
    /// <para>2. actorType does not implement an interface deriving from <see cref="IActor"/> and is not marked as abstract.</para>
    /// </remarks>
    [Obsolete("Use Get(Type, string) instead. This will be removed in the future.")]
    public static bool TryGet(Type actorType, out ActorTypeInformation actorTypeInformation)
    {
        try
        {
            actorTypeInformation = Get(actorType, actorTypeName: null);
            return true;
        }
        catch (ArgumentException)
        {
            actorTypeInformation = null;
            return false;
        }
    }

    /// <summary>
    /// Creates an <see cref="ActorTypeInformation"/> from actorType.
    /// </summary>
    /// <param name="actorType">The type of class implementing the actor to create ActorTypeInformation for.</param>
    /// <returns><see cref="ActorTypeInformation"/> created from actorType.</returns>
    /// <exception cref="System.ArgumentException">
    /// <para>When <see cref="System.Type.BaseType"/> for actorType is not of type <see cref="Actor"/>.</para>
    /// <para>When actorType does not implement an interface deriving from <see cref="IActor"/>
    /// and is not marked as abstract.</para>
    /// </exception>
    [Obsolete("Use Get(Type, string) instead. This will be removed in the future.")]
    public static ActorTypeInformation Get(Type actorType)
    {
        return Get(actorType, actorTypeName: null);
    }

    /// <summary>
    /// Creates an <see cref="ActorTypeInformation"/> from actorType.
    /// </summary>
    /// <param name="actorType">The type of class implementing the actor to create ActorTypeInformation for.</param>
    /// <param name="actorTypeName">The name of the actor type represented by the actor.</param>
    /// <returns><see cref="ActorTypeInformation"/> created from actorType.</returns>
    /// <exception cref="System.ArgumentException">
    /// <para>When <see cref="System.Type.BaseType"/> for actorType is not of type <see cref="Actor"/>.</para>
    /// <para>When actorType does not implement an interface deriving from <see cref="IActor"/>
    /// and is not marked as abstract.</para>
    /// </exception>
    /// <remarks>The value of <paramref name="actorTypeName"/> will have precedence over the default actor type name derived from the actor implementation type or any type name set via <see cref="ActorAttribute"/>.</remarks>
    public static ActorTypeInformation Get(Type actorType, string actorTypeName)
    {
        if (!actorType.IsActor())
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    SR.ErrorNotAnActor,
                    actorType.FullName,
                    typeof(Actor).FullName),
                "actorType");
        }

        // get all actor interfaces
        var actorInterfaces = actorType.GetActorInterfaces();

        // ensure that the if the actor type is not abstract it implements at least one actor interface
        if ((actorInterfaces.Length == 0) && (!actorType.GetTypeInfo().IsAbstract))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    SR.ErrorNoActorInterfaceFound,
                    actorType.FullName,
                    typeof(IActor).FullName),
                "actorType");
        }

        var actorAttribute = actorType.GetCustomAttribute<ActorAttribute>();

        actorTypeName ??= actorAttribute?.TypeName ?? actorType.Name;

        return new ActorTypeInformation()
        {
            ActorTypeName = actorTypeName,
            InterfaceTypes = actorInterfaces,
            ImplementationType = actorType,
            IsAbstract = actorType.GetTypeInfo().IsAbstract,
            IsRemindable = actorType.IsRemindableActor(),
        };
    }
}