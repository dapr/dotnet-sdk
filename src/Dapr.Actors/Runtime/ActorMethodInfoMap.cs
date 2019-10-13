// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Actor method dispatcher map for non remoting calls. method_name -> MethodInfo for methods defined in IACtor interfaces.
    /// </summary>
    internal class ActorMethodInfoMap
    {
        private readonly Dictionary<string, MethodInfo> methods;

        public ActorMethodInfoMap(IEnumerable<Type> interfaceTypes)
        {
            this.methods = new Dictionary<string, MethodInfo>();

            // Find methods which are defined in IActor interface.
            foreach (var actorInterface in interfaceTypes)
            {
                foreach (var methodInfo in actorInterface.GetMethods())
                {
                    this.methods.Add(methodInfo.Name, methodInfo);
                }
            }
        }

        public MethodInfo LookupActorMethodInfo(string methodName)
        {
            if (!this.methods.TryGetValue(methodName, out var methodInfo))
            {
                throw new MissingMethodException($"Actor type doesn't contain method {methodName}");
            }

            return methodInfo;
        }
    }
}
