// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ActorCommunicationClient : IActorCommunicationClient
    {
        private readonly SemaphoreSlim communicationClientLock;
        private readonly IActorCommunicationClientFactory communicationClientFactory;
        private readonly IActorMessageBodyFactory messageBodyFactory;
        private IActionsInteractor actionsInteractor;

        public ActorCommunicationClient(
            IActorCommunicationClientFactory remotingClientFactory,
            ActorId actorId,
            string actorType)
        {
            this.ActorId = actorId;
            this.ActorType = actorType;
            this.communicationClientFactory = remotingClientFactory;
            this.communicationClientLock = new SemaphoreSlim(1);
            this.messageBodyFactory = remotingClientFactory.GetRemotingMessageBodyFactory();
        }

        /// <summary>
        /// Gets the Actor id.
        /// </summary>
        /// <value>actor id.</value>
        public ActorId ActorId { get; }

        /// <summary>
        /// Gets the Actor implementation type name for the actor.
        /// belongs to.
        /// </summary>
        /// <value>Actor implementation type name.</value>
        public string ActorType { get; }

        public async Task<IActorResponseMessage> InvokeAsync(
            IActorRequestMessage remotingRequestMessage,
            string methodName,
            CancellationToken cancellationToken)
        {
              var client = await this.GetCommunicationClientAsync(cancellationToken);
              return await client.InvokeActorMethodWithRemotingAsync(remotingRequestMessage);
        }

        private async Task<IActionsInteractor> GetCommunicationClientAsync(CancellationToken cancellationToken)
        {
            IActionsInteractor client;
            await this.communicationClientLock.WaitAsync(cancellationToken);
            try
            {
                if (this.actionsInteractor == null)
                {
                    this.actionsInteractor = await this.communicationClientFactory.GetClientAsync();
                }

                client = this.actionsInteractor;
            }
            finally
            {
                // Release the lock incase of exceptions from the GetClientAsync method, which can
                // happen if there are non retriable exceptions in that method. Eg: There can be
                // ServiceNotFoundException if the GetClientAsync client is called before the
                // service creation completes.
                this.communicationClientLock.Release();
            }

            return client;
        }
    }
}
