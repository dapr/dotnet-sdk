// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{
    using System;
    using System.Net.Http;
    using Microsoft.Actions.Actors.Builder;
    using Microsoft.Actions.Actors.Communication.Client;
    using Microsoft.Actions.Actors.Runtime;

    /// <summary>
    /// Represents a factory class to create a proxy to the remote actor objects.
    /// </summary>
    internal class ActorProxyFactory : IActorProxyFactory
    {
        private readonly object thisLock;

        private volatile IActorCommunicationClientFactory actorCommunicationClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
        /// TODO: Accept Retry settings.
        /// </summary>
        public ActorProxyFactory()
        {
            this.thisLock = new object();

            this.actorCommunicationClientFactory = null;
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
            return new ActorProxy(actorId, actorType);
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
            var factory = this.GetOrCreateActorCommunicationClientFactory();

            // TODO factory level settings or method level parameter, default http
            var actorCommunicationClient = new ActorCommunicationClient(
                factory,
                actorId,
                actorType);

            var proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(actorInterfaceType);

            return proxyGenerator.CreateActorProxy(
                actorCommunicationClient,
                factory.GetRemotingMessageBodyFactory());
        }

        private IActorCommunicationClientFactory GetOrCreateActorCommunicationClientFactory()
        {
            if (this.actorCommunicationClientFactory != null)
            {
                return this.actorCommunicationClientFactory;
            }

            lock (this.thisLock)
            {
                if (this.actorCommunicationClientFactory == null)
                {
                    this.actorCommunicationClientFactory = this.CreateActorCommunicationClientFactory();
                }
            }

            return this.actorCommunicationClientFactory;
        }

        private IActorCommunicationClientFactory CreateActorCommunicationClientFactory()
        {
            // TODO factory settings
            var factory = new ActorCommunicationClientFactory();
            if (factory == null)
            {
                throw new NotSupportedException("ClientFactory can't be null");
            }

            return factory;
        }
    }
}