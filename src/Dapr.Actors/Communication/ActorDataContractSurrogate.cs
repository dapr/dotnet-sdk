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

namespace Dapr.Actors.Communication
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

#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
        public object? GetObjectToSerialize(object? obj, Type targetType)
#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
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

#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
        public object? GetDeserializedObject(object? obj, Type? targetType)
#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
        {
            if (obj == null)
            {
                return null;
            }
            
            if (obj is IActorReference reference && 
                    typeof(IActor).IsAssignableFrom(targetType) &&
                     !typeof(IActorReference).IsAssignableFrom(targetType))
            {
                return reference.Bind(targetType);
            }

            return obj;
        }
    }
}
