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