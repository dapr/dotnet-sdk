// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;

    internal class ActorCommunicationClient : IActorCommunicationClient
    {
        private readonly SemaphoreSlim communicationClientLock;
        private readonly IActorCommunicationClientFactory communicationClientFactory;
        private IActorMessageBodyFactory messageBodyFactory;
        private IActorCommunicationClient communicationClient;
        private IActionsInteractor actionsInteractor;

        public ActorCommunicationClient(
            IActorCommunicationClientFactory remotingClientFactory,
            IActionsInteractor actionsInteractor,
            ActorId actorId,
            string actorType)
        {
            this.ActorId = actorId;
            this.ActorType = actorType;
            this.communicationClientFactory = remotingClientFactory;
            this.actionsInteractor = actionsInteractor;
            this.communicationClientLock = new SemaphoreSlim(1);
            this.communicationClient = default(IActorCommunicationClient);
            this.messageBodyFactory = remotingClientFactory.GetRemotingMessageBodyFactory();
        }

        /// <summary>
        /// Gets the Actor id. Actor id is used to identify the partition of the service that this actor
        /// belongs to.
        /// </summary>
        /// <value>actor id.</value>
        public ActorId ActorId { get; }

        /// <summary>
        /// Gets the Actor Type that this actor
        /// belongs to.
        /// </summary>
        /// <value>actor id.</value>
        public string ActorType { get; }

        public async Task<IActorResponseMessage> InvokeAsync(
            IActorRequestMessage remotingRequestMessage,
            string methodName,
            CancellationToken cancellationToken)
        {
              var client = await this.GetCommunicationClientAsync(cancellationToken);
              return await client.RequestResponseAsync(remotingRequestMessage);
        }

        public Task<IActorResponseMessage> RequestResponseAsync(IActorRequestMessage requestRequestMessage)
        {
            throw new NotImplementedException();
        }

        public void SendOneWay(IActorRequestMessage requestMessage)
        {
            throw new NotImplementedException();
        }

        private async Task<IActorCommunicationClient> GetCommunicationClientAsync(CancellationToken cancellationToken)
        {
            IActorCommunicationClient client;
            await this.communicationClientLock.WaitAsync(cancellationToken);
            try
            {
                if (this.communicationClient == null)
                {
                    this.communicationClient = await this.communicationClientFactory.GetClientAsync(this.actionsInteractor, cancellationToken);
                }

                client = this.communicationClient;
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
