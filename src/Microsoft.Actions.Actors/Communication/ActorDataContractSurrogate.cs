// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.Runtime.Serialization;

    internal class ActorDataContractSurrogate : ISerializationSurrogateProvider
    {
        public static readonly ISerializationSurrogateProvider Instance = new ActorDataContractSurrogate();

        public Type GetSurrogateType(Type type)
        {
            if (typeof(IActor).IsAssignableFrom(type))
            {
                return typeof(ActorReference);
            }

            return type;
        }

        public object GetObjectToSerialize(object obj, Type targetType)
        {
            if (obj == null)
            {
                return null;
            }
            else if (obj is IActor)
            {
                return ActorReference.Get(obj);
            }

            return obj;
        }

        public object GetDeserializedObject(object obj, Type targetType)
        {
            if (obj == null)
            {
                return null;
            }
            else if (obj is IActorReference && typeof(IActor).IsAssignableFrom(targetType) &&
                     !typeof(IActorReference).IsAssignableFrom(targetType))
            {
                return ((IActorReference)obj).Bind(targetType);
            }

            return obj;
        }
    }
}
