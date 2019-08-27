// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client.Communication
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

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
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding operation. The result of the Task is
        /// the CommunicationClient(<see cref="IActorCommunicationClient" />) object.
        /// </returns>
        public async Task<IActionsInteractor> GetClientAsync()
        {
            return await this.CreateClientAsync();
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
        /// <returns>The communication client that was created.</returns>
        private Task<IActionsInteractor> CreateClientAsync()
        {
            try
            {
                // TODO add retries and error handling - add CreateClientWithRetriesAsync version
                var client = new ActionsHttpInteractor(
                    this.serializersManager);
                return Task.FromResult((IActionsInteractor)client);
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
