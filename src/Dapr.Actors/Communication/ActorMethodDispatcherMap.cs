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

namespace Dapr.Actors.Communication;

using System;
using System.Collections.Generic;
using System.Globalization;
using Dapr.Actors.Builder;
using Dapr.Actors.Resources;

/// <summary>
/// Actor method dispatcher map for remoting calls.
/// </summary>
internal class ActorMethodDispatcherMap
{
    private readonly IDictionary<int, ActorMethodDispatcherBase> map;

    public ActorMethodDispatcherMap(IEnumerable<Type> interfaceTypes)
    {
        this.map = new Dictionary<int, ActorMethodDispatcherBase>();

        foreach (var actorInterfaceType in interfaceTypes)
        {
            var methodDispatcher = ActorCodeBuilder.GetOrCreateMethodDispatcher(actorInterfaceType);
            this.map.Add(methodDispatcher.InterfaceId, methodDispatcher);
        }
    }

    public ActorMethodDispatcherBase GetDispatcher(int interfaceId)
    {
        if (!this.map.TryGetValue(interfaceId, out var methodDispatcher))
        {
            throw new KeyNotFoundException(string.Format(
                CultureInfo.CurrentCulture,
                SR.ErrorMethodDispatcherNotFound,
                interfaceId));
        }

        return methodDispatcher;
    }
}