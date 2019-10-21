// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication.Client
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ActorNonRemotingClient
    {
        private readonly IDaprInteractor daprInteractor;

        public ActorNonRemotingClient(IDaprInteractor daprInteractor)
        {
            this.daprInteractor = daprInteractor;
        }

        /// <summary>
        /// Invokes an Actor method on Dapr runtime without remoting.
        /// </summary>
        /// <param name="actorType">Type of actor.</param>
        /// <param name="actorId">ActorId.</param>
        /// <param name="methodName">Method name to invoke.</param>
        /// <param name="jsonPayload">Serialized body.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default)
        {
            return this.daprInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, jsonPayload, cancellationToken);
        }
    }
}
