// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime.Communication
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Actions.Actors.Builder;
    using Microsoft.Actions.Actors.Resources;

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

        public ActorMethodDispatcherBase GetDispatcher(int interfaceId, int methodId)
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
}
