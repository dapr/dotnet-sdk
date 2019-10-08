// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Builder
{
    using System;
    using Microsoft.Dapr.Actors.Client;
    using Microsoft.Dapr.Actors.Communication;
    using Microsoft.Dapr.Actors.Communication.Client;

    internal class ActorProxyGenerator
    {
        private readonly IProxyActivator proxyActivator;

        public ActorProxyGenerator(
            Type proxyInterfaceType,
            IProxyActivator proxyActivator)
        {
            this.proxyActivator = proxyActivator;
            this.ProxyInterfaceType = proxyInterfaceType;
        }

        public Type ProxyInterfaceType { get; }

        public ActorProxy CreateActorProxy()
        {
            return (ActorProxy)this.proxyActivator.CreateInstance();
        }
    }
}
