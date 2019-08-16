// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using System;
    using Microsoft.Actions.Actors.Client;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Communication.Client;

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

        public ActorProxy CreateActorProxy(
            ActorCommunicationClient remotingPartitionClient,
            IActorMessageBodyFactory remotingMessageBodyFactory)
        {
            var serviceProxy = (ActorProxy)this.proxyActivator.CreateInstance();
            serviceProxy.Initialize(remotingPartitionClient, remotingMessageBodyFactory);
            return serviceProxy;
        }
    }
}
