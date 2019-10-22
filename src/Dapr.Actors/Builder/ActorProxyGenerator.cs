// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System;
    using Dapr.Actors.Client;

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
