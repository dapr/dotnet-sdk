// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    /// <summary>
    /// This is the implmentation  for <see cref="IActorCommunicationMessageSerializationProvider"/>used by remoting service and client during
    /// request/response serialization . It uses request Wrapping and data contract for serialization.
    /// </summary>
    public class ActorCommunicationWrappingDataContractSerializationProvider : IActorCommunicationMessageSerializationProvider
    {
        private static readonly IEnumerable<Type> DefaultKnownTypes = new[]
        {
            typeof(ActorReference),
        };

        private readonly ActorCommunicationDataContractSerializationProvider internalprovider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorCommunicationWrappingDataContractSerializationProvider"/> class
        /// with default IBufferPoolManager implementation.
        /// </summary>
        public ActorCommunicationWrappingDataContractSerializationProvider()
        {
            this.internalprovider = new ActorCommunicationDataContractSerializationProvider();
        }

        /// <summary>
        /// Creates a MessageFactory for Wrapped Message DataContract Remoting Types. This is used to create Remoting Request/Response objects.
        /// </summary>
        /// <returns>
        /// <see cref="IMessageBodyFactory" /> that provides an instance of the factory for creating
        /// remoting request and response message bodies.
        /// </returns>
        public IMessageBodyFactory CreateMessageBodyFactory()
        {
            return new WrappedRequestMessageFactory();
        }

        /// <summary>
        /// Creates IServiceRemotingRequestMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
        /// </summary>
        /// <param name="serviceInterfaceType">The remoted service interface.</param>
        /// <param name="methodParameterTypes">The union of parameter types of all of the methods of the specified interface.</param>
        /// <param name="wrappedMessageTypes">Wrapped Request Types for all Methods.</param>
        /// <returns>
        /// An instance of the <see cref="IActorCommunicationRequestMessageBodySerializer" /> that can serialize the service
        /// remoting request message body to a messaging body for transferring over the transport.
        /// </returns>
        public IActorCommunicationRequestMessageBodySerializer CreateRequestMessageSerializer(
            Type serviceInterfaceType,
            IEnumerable<Type> methodParameterTypes,
            IEnumerable<Type> wrappedMessageTypes = null)
        {
            DataContractSerializer serializer = this.CreateRemotingRequestMessageBodyDataContractSerializer(
                typeof(WrappedMessageBody),
                wrappedMessageTypes);

            return this.internalprovider.CreateRemotingRequestMessageSerializer<WrappedMessageBody, WrappedMessageBody>(
              serializer);
        }

        /// <summary>
        /// Creates IServiceRemotingResponseMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
        /// </summary>
        /// <param name="serviceInterfaceType">The remoted service interface.</param>
        /// <param name="methodReturnTypes">The return types of all of the methods of the specified interface.</param>
        /// <param name="wrappedMessageTypes">Wrapped Response Types for all remoting methods.</param>
        /// <returns>
        /// An instance of the <see cref="IActorCommunicationResponseMessageBodySerializer" /> that can serialize the service
        /// remoting response message body to a messaging body for transferring over the transport.
        /// </returns>
        public IActorCommunicationResponseMessageBodySerializer CreateResponseMessageSerializer(
            Type serviceInterfaceType,
            IEnumerable<Type> methodReturnTypes,
            IEnumerable<Type> wrappedMessageTypes = null)
        {
            DataContractSerializer serializer = this.CreateRemotingResponseMessageBodyDataContractSerializer(
                typeof(WrappedMessageBody),
                wrappedMessageTypes);
            return this.internalprovider
                .CreateRemotingResponseMessageSerializer<WrappedMessageBody, WrappedMessageBody>(
                    serializer);
        }

        /// <summary>
        ///     Create the writer to write to the stream. Use this method to customize how the serialized contents are written to
        ///     the stream.
        /// </summary>
        /// <param name="outputStream">The stream on which to write the serialized contents.</param>
        /// <returns>
        ///     An <see cref="System.Xml.XmlDictionaryWriter" /> using which the serializer will write the object on the
        ///     stream.
        /// </returns>
        internal XmlDictionaryWriter CreateXmlDictionaryWriter(Stream outputStream)
        {
            return this.internalprovider.CreateXmlDictionaryWriter(outputStream);
        }

        /// <summary>
        ///     Create the reader to read from the input stream. Use this method to customize how the serialized contents are read
        ///     from the stream.
        /// </summary>
        /// <param name="inputStream">The stream from which to read the serialized contents.</param>
        /// <returns>
        ///     An <see cref="System.Xml.XmlDictionaryReader" /> using which the serializer will read the object from the
        ///     stream.
        /// </returns>
        internal XmlDictionaryReader CreateXmlDictionaryReader(Stream inputStream)
        {
            return this.internalprovider.CreateXmlDictionaryReader(inputStream);
        }

        /// <summary>
        ///     Gets the settings used to create DataContractSerializer for serializing and de-serializing request message body.
        /// </summary>
        /// <param name="remotingRequestType">Remoting RequestMessageBody Type.</param>
        /// <param name="knownTypes">The return types of all of the methods of the specified interface.</param>
        /// <returns><see cref="DataContractSerializerSettings" /> for serializing and de-serializing request message body.</returns>
        internal DataContractSerializer CreateRemotingRequestMessageBodyDataContractSerializer(
            Type remotingRequestType,
            IEnumerable<Type> knownTypes)
        {
            var serializer = this.internalprovider.CreateRemotingRequestMessageBodyDataContractSerializer(
                remotingRequestType,
                knownTypes);

            serializer.SetSerializationSurrogateProvider(new ActorDataContractSurrogate());
            return serializer;
        }

        /// <summary>
        ///     Gets the settings used to create DataContractSerializer for serializing and de-serializing request message body.
        /// </summary>
        /// <param name="remotingResponseType">Remoting ResponseMessage Type.</param>
        /// <param name="knownTypes">The return types of all of the methods of the specified interface.</param>
        /// <returns><see cref="DataContractSerializerSettings" /> for serializing and de-serializing request message body.</returns>
        internal DataContractSerializer CreateRemotingResponseMessageBodyDataContractSerializer(
            Type remotingResponseType,
            IEnumerable<Type> knownTypes)
        {
            var serializer = this.internalprovider.CreateRemotingResponseMessageBodyDataContractSerializer(
                remotingResponseType,
                knownTypes);

            serializer.SetSerializationSurrogateProvider(new ActorDataContractSurrogate());
            return serializer;
        }

        private static IEnumerable<Type> AddDefaultKnownTypes(IEnumerable<Type> knownTypes)
        {
            var types = new List<Type>(knownTypes);
            types.AddRange(DefaultKnownTypes);
            return types;
        }
    }
}
