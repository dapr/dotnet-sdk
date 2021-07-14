// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Description
{
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
}
