// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Represents a factory class to create a proxy to the remote actor objects.
    /// </summary>
    public class ActorProxyFactory : IActorProxyFactory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
        /// TODO: Accept Retry settings.
        /// </summary>
        public ActorProxyFactory()
        {
            // TODO: Configure HttpClient properties.
            this.HttpClient = new HttpClient();
        }

        internal HttpClient HttpClient { get; }

        /// <inheritdoc/>
        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, Type actorType) 
            where TActorInterface : IActor
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public ActorProxy CreateActorProxy(ActorId actorId, Type actorType)
        {
            throw new NotImplementedException();
        }
    }
}