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
        private readonly IActionsInteractor actionsInteractor;

        public ActorCommunicationClient(
            IActionsInteractor actionsInteractor,
            ActorId actorId,
            string actorType)
        {
            this.ActorId = actorId;
            this.ActorType = actorType;
            this.actionsInteractor = actionsInteractor;
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
              return await this.actionsInteractor.InvokeActorMethodWithRemotingAsync(remotingRequestMessage);
        }
    }
}
