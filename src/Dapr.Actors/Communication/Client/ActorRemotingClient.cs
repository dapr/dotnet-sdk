// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication.Client
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class ActorRemotingClient
    {
        private readonly ActorMessageSerializersManager serializersManager;
        private readonly IActorMessageBodyFactory remotingMessageBodyFactory = null;
        private readonly IDaprInteractor daprInteractor;

        public ActorRemotingClient(
            IDaprInteractor daprInteractor,
            IActorMessageBodySerializationProvider serializationProvider = null)
        {
            this.daprInteractor = daprInteractor;
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
            CancellationToken cancellationToken)
        {
              return await this.daprInteractor.InvokeActorMethodWithRemotingAsync(this.serializersManager, remotingRequestMessage, cancellationToken);
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
