// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ActorRemotingClient
    {                
        private readonly ActorMessageSerializersManager serializersManager;        
        private readonly IActorMessageBodyFactory remotingMessageBodyFactory = null;
        private readonly IActionsInteractor actionsInteractor;

        public ActorRemotingClient(
            IActionsInteractor actionsInteractor,
            IActorMessageBodySerializationProvider serializationProvider = null)
        {
            this.actionsInteractor = actionsInteractor;
            this.serializersManager = IntializeSerializationManager(serializationProvider);
            this.remotingMessageBodyFactory = this.serializersManager.GetSerializationProvider().CreateMessageBodyFactory();
        }

        /// <summary>
        /// Gets a factory for creating the remoting message bodies.
        /// </summary>
        /// <returns>A factory for creating the remoting message bodies.</returns>
        public IActorMessageBodyFactory GetRemotingMessageBodyFactory()
        {
            return this.remotingMessageBodyFactory;
        }

        public async Task<IActorResponseMessage> InvokeAsync(
            IActorRequestMessage remotingRequestMessage,
            string methodName,
            CancellationToken cancellationToken)
        {
              return await this.actionsInteractor.InvokeActorMethodWithRemotingAsync(this.serializersManager, remotingRequestMessage, cancellationToken);
        }

        private static ActorMessageSerializersManager IntializeSerializationManager(
            IActorMessageBodySerializationProvider serializationProvider)
        {
            // TODO serializer settings 
            return new ActorMessageSerializersManager(
                serializationProvider,
                new ActorMessageHeaderSerializer());
        }
    }
}
