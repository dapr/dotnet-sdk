// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Contains extension method for Actor interface.
    /// </summary>
    internal static class ActorInterfaceExtensions
    {
        /// <summary>
        /// Gets the actor interfaces implemented by the actor class.
        /// </summary>
        /// <param name="type">The type of class implementing actor.</param>
        /// <returns>An array containing actor interface which the type implements.</returns>
        public static Type[] GetActorInterfaces(this Type type)
        {
            var list = new List<Type>(type.GetInterfaces().Where(t => typeof(IActor).IsAssignableFrom(t)));
            list.RemoveAll(t => (t.GetNonActorParentType() != null));

            return list.ToArray();
        }

        /// <summary>
        /// Indicates whether the interface type is an actor interface.
        /// </summary>
        /// <param name="actorInterfaceType">The interface type of the actor.</param>
        /// <returns>true, if the actorInterfaceType is an interface only implements <see cref="IActor"/>.</returns>
        public static bool IsActorInterface(this Type actorInterfaceType)
        {
            return (actorInterfaceType.GetTypeInfo().IsInterface && (actorInterfaceType.GetNonActorParentType() == null));
        }

        public static Type GetNonActorParentType(this Type type)
        {
            var list = new List<Type>(type.GetInterfaces());

            // must have IActor as the parent, so removal of it should result in reduction in the count.
            if (list.RemoveAll(t => (t == typeof(IActor))) == 0)
            {
                return type;
            }

            foreach (var t in list)
            {
                var nonActorParent = GetNonActorParentType(t);
                if (nonActorParent != null)
                {
                    return nonActorParent;
                }
            }

            return null;
        }
    }
}
