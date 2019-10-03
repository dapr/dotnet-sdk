// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Client
{
    using System;
    using System.Net.Http;
    using Microsoft.Dapr.Actors.Builder;
    using Microsoft.Dapr.Actors.Communication.Client;
    using Microsoft.Dapr.Actors.Runtime;

    /// <summary>
    /// Represents a factory class to create a proxy to the remote actor objects.
    /// </summary>
    internal class ActorProxyFactory : IActorProxyFactory
    {
        private readonly IDaprInteractor daprInteractor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
        /// TODO: Accept Retry settings.
        /// </summary>
        public ActorProxyFactory()
        {
            // TODO: Allow configuration of serialization and client settings.
            this.daprInteractor = new DaprHttpInteractor();
        }

        /// <inheritdoc/>
        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, string actorType) 
            where TActorInterface : IActor
        {
            return (TActorInterface)this.CreateActorProxy(actorId, typeof(TActorInterface), actorType);
        }

        /// <inheritdoc/>
        public ActorProxy Create(ActorId actorId, string actorType)
        {
            var actorProxy = new ActorProxy();
            var nonRemotingClient = new ActorNonRemotingClient(this.daprInteractor);
            actorProxy.Initialize(nonRemotingClient, actorId, actorType);

            return actorProxy;
        }

        /// <summary>
        /// Create a proxy, this method is also sued by ACtorReference also to create proxy.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorInterfaceType">Actor Interface Type.</param>
        /// <param name="actorType">Actor implementation Type.</param>
        /// <returns>Returns Actor Proxy.</returns>
        internal object CreateActorProxy(ActorId actorId, Type actorInterfaceType, string actorType)
        {
            var remotingClient = new ActorRemotingClient(this.daprInteractor);
            var proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(actorInterfaceType);
            var actorProxy = proxyGenerator.CreateActorProxy();
            actorProxy.Initialize(remotingClient, actorId, actorType);

            return actorProxy;
        }        
    }
}