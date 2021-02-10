// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using System;
    using System.Net.Http;
    using Dapr.Actors.Builder;
    using Dapr.Actors.Communication.Client;

    /// <summary>
    /// Represents a factory class to create a proxy to the remote actor objects.
    /// </summary>
    public class ActorProxyFactory : IActorProxyFactory
    {
        private ActorProxyOptions defaultOptions;
        private readonly HttpClientHandler handler;

        /// <inheritdoc/>
        public ActorProxyOptions DefaultOptions
        {
            get => this.defaultOptions;
            set
            {
                this.defaultOptions = value ??
                    throw new ArgumentNullException(nameof(DefaultOptions), $"{nameof(ActorProxyFactory)}.{nameof(DefaultOptions)} cannot be null");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxyFactory"/> class.
        /// </summary>
        public ActorProxyFactory(ActorProxyOptions options = null, HttpClientHandler handler = null)
        {
            this.defaultOptions = options ?? new ActorProxyOptions();
            this.handler = handler;
        }

        /// <inheritdoc/>
        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, string actorType, ActorProxyOptions options = null, IDaprInteractor daprInteractor = null)
            where TActorInterface : IActor
            => (TActorInterface)this.CreateActorProxy(actorId, typeof(TActorInterface), actorType, options ?? this.defaultOptions, daprInteractor);

        /// <inheritdoc/>
        public ActorProxy Create(ActorId actorId, string actorType, ActorProxyOptions options = null, IDaprInteractor daprInteractor = null)
        {
            var actorProxy = new ActorProxy();
            daprInteractor ??= new DaprHttpInteractor(this.handler, this.DefaultOptions.DaprApiToken);
            var nonRemotingClient = new ActorNonRemotingClient(daprInteractor);
            actorProxy.Initialize(nonRemotingClient, actorId, actorType, options ?? this.defaultOptions);

            return actorProxy;
        }

        /// <inheritdoc/>
        public object CreateActorProxy(ActorId actorId, Type actorInterfaceType, string actorType, ActorProxyOptions options = null, IDaprInteractor daprInteractor = null)
        {
            daprInteractor ??= new DaprHttpInteractor(this.handler, this.DefaultOptions.DaprApiToken);
            var remotingClient = new ActorRemotingClient(daprInteractor);
            var proxyGenerator = ActorCodeBuilder.GetOrCreateProxyGenerator(actorInterfaceType);
            var actorProxy = proxyGenerator.CreateActorProxy();
            actorProxy.Initialize(remotingClient, actorId, actorType, options ?? this.defaultOptions);

            return actorProxy;
        }
    }
}
