// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;

    /// <summary>
    /// An <see cref="IActorCommunicationClientFactory"/> that uses
    /// http protocol to create <see cref="IActorCommunicationClient"/> that communicate with actors.
    /// </summary>
    internal class ActorCommunicationClientFactory : IActorCommunicationClientFactory
    {
        private readonly ActorMessageSerializersManager serializersManager;
        private IActorMessageBodyFactory remotingMessageBodyFactory = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorCommunicationClientFactory"/> class.
        ///     Constructs a fabric transport based service remoting client factory.
        /// </summary>
        /// <param name="serializationProvider">IActorCommunicationMessageSerializationProvider provider.</param>
        public ActorCommunicationClientFactory(
            IActorMessageBodySerializationProvider serializationProvider = null)
        {
            // TODO  Add settings, exception handlers, serialization provider
            this.serializersManager = IntializeSerializationManager(serializationProvider);
            this.remotingMessageBodyFactory = this.serializersManager.GetSerializationProvider().CreateMessageBodyFactory();
        }

        /// <summary>
        /// Returns a client to communicate.
        /// </summary>
        /// <param name="actionInteractor">Action Interactor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding operation. The result of the Task is
        /// the CommunicationClient(<see cref="IActorCommunicationClient" />) object.
        /// </returns>
        public async Task<IActorCommunicationClient> GetClientAsync(IActionsInteractor actionInteractor, CancellationToken cancellationToken)
        {
            return await this.CreateClientAsync(actionInteractor, cancellationToken);
        }

        /// <summary>
        /// Gets a factory for creating the remoting message bodies.
        /// </summary>
        /// <returns>A factory for creating the remoting message bodies.</returns>
        public IActorMessageBodyFactory GetRemotingMessageBodyFactory()
        {
            return this.remotingMessageBodyFactory;
        }

        private static ActorMessageSerializersManager IntializeSerializationManager(
            IActorMessageBodySerializationProvider serializationProvider)
        {
            // TODO serializer settings 
            return new ActorMessageSerializersManager(
                serializationProvider,
                new ActorMessageHeaderSerializer());
        }

        /// <summary>
        /// Creates a communication client for the given endpoint address.
        /// </summary>
        /// <param name="actionsHttpInteractor">Actions Interactor.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The communication client that was created.</returns>
        private Task<IActorCommunicationClient> CreateClientAsync(
            IActionsInteractor actionsHttpInteractor,
            CancellationToken cancellationToken)
        {
            try
            {
                // TODO add retries and error handling - add CreateClientWithRetriesAsync version
                var client = new HttpActorCommunicationClient(
                    this.serializersManager,
                    actionsHttpInteractor);
                return Task.FromResult((IActorCommunicationClient)client);
            }
            catch (Exception ex)
            {
                // TODO specific error handling
                throw new Exception(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ex.ToString()));
            }
        }
    }
}
