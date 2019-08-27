// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    /// <summary>
    /// An <see cref="IActorCommunicationClientFactory"/> that uses
    /// http protocol to create <see cref="IActorCommunicationClient"/> that communicate with actors.
    /// </summary>
    internal class ActorCommunicationClientFactory : IActorCommunicationClientFactory
    {
        private static readonly IActionsInteractor ActionsInteractor = new ActionsHttpInteractor();
        private readonly ActorMessageSerializersManager serializersManager;
        private readonly IActorMessageBodyFactory actorMessageBodyFactory = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorCommunicationClientFactory"/> class.
        /// Constructs actor remoting communication client factory.
        /// </summary>
        /// <param name="serializationProvider">IActorCommunicationMessageSerializationProvider provider.</param>
        public ActorCommunicationClientFactory(
            IActorMessageBodySerializationProvider serializationProvider = null)
        {
            // TODO  Add settings, exception handlers, serialization provider
            this.serializersManager = IntializeSerializationManager(serializationProvider);
            this.actorMessageBodyFactory = this.serializersManager.GetSerializationProvider().CreateMessageBodyFactory();
        }

        /// <summary>
        /// Gets a factory for creating the remoting message bodies.
        /// </summary>
        /// <returns>A factory for creating the remoting message bodies.</returns>
        public IActorMessageBodyFactory GetRemotingMessageBodyFactory()
        {
            return this.actorMessageBodyFactory;
        }

        public ActorCommunicationClient GetClient(ActorId actorId, string actorType)
        {
            return new ActorCommunicationClient(ActionsInteractor, actorId, actorType);
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
