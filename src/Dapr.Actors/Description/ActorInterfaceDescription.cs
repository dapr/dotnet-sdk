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

namespace Dapr.Actors.Description;

using System;
using System.Globalization;
using System.Reflection;
using Dapr.Actors;
using Dapr.Actors.Resources;
using Dapr.Actors.Runtime;

internal class ActorInterfaceDescription : InterfaceDescription
{
    private ActorInterfaceDescription(Type actorInterfaceType, bool useCRCIdGeneration)
        : base("actor", actorInterfaceType, useCRCIdGeneration, MethodReturnCheck.EnsureReturnsTask)
    {
    }

    public static ActorInterfaceDescription Create(Type actorInterfaceType)
    {
        EnsureActorInterface(actorInterfaceType);
        return new ActorInterfaceDescription(actorInterfaceType, false);
    }

    public static ActorInterfaceDescription CreateUsingCRCId(Type actorInterfaceType)
    {
        EnsureActorInterface(actorInterfaceType);

        return new ActorInterfaceDescription(actorInterfaceType, true);
    }

    private static void EnsureActorInterface(Type actorInterfaceType)
    {
        if (!actorInterfaceType.GetTypeInfo().IsInterface)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    SR.ErrorNotAnActorInterface_InterfaceCheck,
                    actorInterfaceType.FullName,
                    typeof(IActor).FullName),
                "actorInterfaceType");
        }

        var nonActorParentInterface = actorInterfaceType.GetNonActorParentType();
        if (nonActorParentInterface != null)
        {
            if (nonActorParentInterface == actorInterfaceType)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        SR.ErrorNotAnActorInterface_DerivationCheck1,
                        actorInterfaceType.FullName,
                        typeof(IActor).FullName),
                    "actorInterfaceType");
            }
            else
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        SR.ErrorNotAnActorInterface_DerivationCheck2,
                        actorInterfaceType.FullName,
                        nonActorParentInterface.FullName,
                        typeof(IActor).FullName),
                    "actorInterfaceType");
            }
        }
    }
}