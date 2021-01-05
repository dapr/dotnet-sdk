// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
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
}
