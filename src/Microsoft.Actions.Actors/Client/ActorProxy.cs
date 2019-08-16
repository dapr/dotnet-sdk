// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{    
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides the base implementation for the proxy to the remote actor objects implementing <see cref="IActor"/> interfaces.
    /// The proxy object can be used used for client-to-actor and actor-to-actor communication.
    /// </summary>
    public class ActorProxy
    {
        internal static readonly ActorProxyFactory DefaultProxyFactory = new ActorProxyFactory();
        private static ActionsHttpInteractor actionsHttpInteractor = new ActionsHttpInteractor();
        private string actorType;
        private ActorId actorId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxy"/> class.
        /// </summary>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        protected ActorProxy(ActorId actorId, string actorType)
        {
            this.actorType = actorType;
            this.actorId = actorId;
        }

        /// <summary>
        /// Creates a proxy to the actor object that implements an actor interface.
        /// </summary>
        /// <typeparam name="TActorInterface">
        /// The actor interface implemented by the remote actor object.
        /// The returned proxy object will implement this interface.
        /// </typeparam>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        /// <returns>Proxy to the actor object.</returns>
        public static TActorInterface Create<TActorInterface>(ActorId actorId, Type actorType) 
            where TActorInterface : IActor
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a proxy to the actor object that doesnt implement the actor interface.
        /// </summary>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        /// <returns>Proxy to the actor object.</returns>
        public static ActorProxy Create(ActorId actorId, string actorType)
        {
            return new ActorProxy(actorId, actorType);
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided json payload.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="data">Object argument for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Json response form server.</returns>
        public Task<string> InvokeAsync(string method, object data, CancellationToken cancellationToken = default(CancellationToken))
        {
            var jsonPayload = JsonConvert.SerializeObject(data);
            return actionsHttpInteractor.InvokeActorMethodAsync(this.actorType, this.actorId, method, jsonPayload, cancellationToken);
        }
    }
}
