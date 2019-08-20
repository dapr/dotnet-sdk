// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    internal class ActorTypeInfo
    {
        public ActorTypeInfo(Type actorType, Func<ActorId, Actor> actorFactory = null)
        {
            this.Methods = new Dictionary<string, MethodInfo>();
            this.ImplementationType = actorType;
            this.IsRemindable = actorType.IsRemindableActor();

            if (actorFactory == null)
            {
                // Use default Activation.
                actorFactory = (actorId) =>
                {
                    return (Actor)Activator.CreateInstance(actorType, actorId);
                };
            }

            this.ActorFactory = actorFactory;

            // Find methods which are defined in IActor interface.
            foreach (var actorInterface in actorType.GetActorInterfaces())
            {                
                foreach (var methodInfo in actorInterface.GetMethods())
                {
                    this.Methods.Add(methodInfo.Name, methodInfo);
                }
            }
        }

        /// <summary>
        /// Gets the type of the class implementing the actor.
        /// </summary>
        /// <value>The <see cref="System.Type"/> of the class implementing the actor.</value>
        public Type ImplementationType { get; }

        /// <summary>
        /// Gets the actor interface types which derive from <see cref="IActor"/> and implemented by actor class.
        /// </summary>
        /// <value>An enumerator that can be used to iterate through the actor interface type.</value>
        public IEnumerable<Type> InterfaceTypes { get; private set; }

        public Dictionary<string, MethodInfo> Methods { get; }

        public Func<ActorId, Actor> ActorFactory { get; }

        /// <summary>
        /// Gets a value indicating whether the actor class implements <see cref="IRemindable"/>.
        /// </summary>
        /// <value>true if the actor class implements <see cref="IRemindable"/>, otherwise false.</value>
        public bool IsRemindable { get; }

        public MethodInfo LookupActorMethodInfo(string methodName)
        {
            if (!this.Methods.TryGetValue(methodName, out var methodInfo))
            { 
                throw new MissingMethodException($"Actor type {this.ImplementationType.Name} doesn't contain method {methodName}");
            }

            return methodInfo;
        }
    }
}
