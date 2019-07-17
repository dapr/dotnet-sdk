// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the base implementation for the proxy to the remote actor objects implementing <see cref="IActor"/> interfaces.
    /// The proxy object can be used used for client-to-actor and actor-to-actor communication.
    /// </summary>
    public class ActorProxy
    {
        internal static readonly ActorProxyFactory DefaultProxyFactory = new ActorProxyFactory();

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxy"/> class.
        /// </summary>
        protected ActorProxy()
        {
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
        public TActorInterface Create<TActorInterface>(ActorId actorId, Type actorType) 
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
        public ActorProxy Create(ActorId actorId, Type actorType)
        {
            return new ActorProxy();
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided json payload.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="json">Json payload for actor method.</param>
        /// <returns>Json response form server.</returns>
        public async Task<string> InvokeAsync(string method, string json)
        {
            await Task.CompletedTask;
            return string.Empty;
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided json payload.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="json">Json payload for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Json response form server.</returns>
        public async Task<string> InvokeAsync(string method, string json, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.CompletedTask;
            return string.Empty;
        }
    }
}
