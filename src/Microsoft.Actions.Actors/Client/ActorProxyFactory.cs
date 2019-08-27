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
        // Used only for Remoting based communication
        private static readonly ActorCommunicationClientFactory DefaultActorCommunicationClientFactory = new ActorCommunicationClientFactory();

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
        /// TODO: Accept Retry settings.
        /// </summary>
        public ActorProxyFactory()
        {
        }

        /// <inheritdoc/>
        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, string actorType) 
            where TActorInterface : IActor
        {
            return (TActorInterface)this.CreateActorProxy(actorId, typeof(TActorInterface), actorType);
        }

        /// <summary>
        /// Create a proxy, this method is also used by ActorReference also to create proxy.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorInterfaceType">Actor Interface Type.</param>
        /// <param name="actorType">Actor implementation Type.</param>
        /// <returns>Returns Actor Proxy.</returns>
        internal object CreateActorProxy(ActorId actorId, Type actorInterfaceType, string actorType)
        {
            // TODO factory/client level settings
            var actorCommunicationClient = DefaultActorCommunicationClientFactory.GetClient(actorId, actorType);

            var proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(actorInterfaceType);

            return proxyGenerator.CreateActorProxy(
                actorCommunicationClient,
                DefaultActorCommunicationClientFactory.GetRemotingMessageBodyFactory());
        }
    }
}