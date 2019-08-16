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
    public class ActorProxyFactory : IActorProxyFactory
    {
        private readonly object thisLock;

        private volatile IActorCommunicationClientFactory actorCommunicationClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
        /// TODO: Accept Retry settings.
        /// </summary>
        public ActorProxyFactory()
        {
            // TODO: Configure HttpClient properties.
            this.HttpClient = new HttpClient();

            this.thisLock = new object();

            this.actorCommunicationClientFactory = null;
        }

        internal HttpClient HttpClient { get; }

        /// <inheritdoc/>
        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, Type actorType) 
            where TActorInterface : IActor
        {
            var actorInterfaceType = typeof(TActorInterface);

            var factory = this.GetOrCreateActorCommunicationClientFactory();

            // TODO factory level settings or method level parameter, default http
            var actorCommunicationClient = new ActorCommunicationClient(
                factory,
                new ActionsHttpInteractor(),
                actorId,
                actorType);

            var proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(actorInterfaceType);

            return (TActorInterface)(object)proxyGenerator.CreateActorProxy(
                actorCommunicationClient,
                factory.GetRemotingMessageBodyFactory());
        }

        /// <inheritdoc/>
        public ActorProxy CreateActorProxy(ActorId actorId, Type actorType)
        {
            throw new NotImplementedException();
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