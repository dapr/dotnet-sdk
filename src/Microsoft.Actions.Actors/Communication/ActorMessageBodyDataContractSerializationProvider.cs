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
    /// This is the implmentation  for <see cref="IActorMessageBodySerializationProvider"/>used by remoting service and client during
    /// request/response serialization . It uses request Wrapping and data contract for serialization.
    /// </summary>
    public class ActorMessageBodyDataContractSerializationProvider : IActorMessageBodySerializationProvider
    {
        private static readonly IEnumerable<Type> DefaultKnownTypes = new[]
        {
            typeof(ActorReference),
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorMessageBodyDataContractSerializationProvider"/> class.
        /// </summary>
        public ActorMessageBodyDataContractSerializationProvider()
        {
        }

        /// <summary>
        /// Creates a MessageFactory for Wrapped Message DataContract Remoting Types. This is used to create Remoting Request/Response objects.
        /// </summary>
        /// <returns>
        /// <see cref="IActorMessageBodyFactory" /> that provides an instance of the factory for creating
        /// remoting request and response message bodies.
        /// </returns>
        public IActorMessageBodyFactory CreateMessageBodyFactory()
        {
            return new WrappedRequestMessageFactory();
        }

        /// <summary>
        /// Creates IActorMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
        /// </summary>
        /// <param name="serviceInterfaceType">The remoted service interface.</param>
        /// <param name="methodParameterTypes">The union of parameter types of all of the methods of the specified interface.</param>
        /// <param name="wrappedMessageTypes">Wrapped Request Types for all Methods.</param>
        /// <returns>
        /// An instance of the <see cref="IActorMessageBodySerializer" /> that can serialize the service
        /// remoting request message body to a messaging body for transferring over the transport.
        /// </returns>
        public IActorMessageBodySerializer CreateMessageBodySerializer(
            Type serviceInterfaceType,
            IEnumerable<Type> methodParameterTypes,
            IEnumerable<Type> wrappedMessageTypes = null)
        {
            DataContractSerializer serializer = this.CreateMessageBodyDataContractSerializer(
                typeof(WrappedMessageBody),
                wrappedMessageTypes);

            return new MemoryStreamMessageBodySerializer(this, serializer);
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
            return XmlDictionaryWriter.CreateBinaryWriter(outputStream);
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
            return XmlDictionaryReader.CreateBinaryReader(inputStream, XmlDictionaryReaderQuotas.Max);
        }

        /// <summary>
        ///     Gets the settings used to create DataContractSerializer for serializing and de-serializing request message body.
        /// </summary>
        /// <param name="remotingRequestType">Remoting RequestMessageBody Type.</param>
        /// <param name="knownTypes">The return types of all of the methods of the specified interface.</param>
        /// <returns><see cref="DataContractSerializerSettings" /> for serializing and de-serializing request message body.</returns>
        internal DataContractSerializer CreateMessageBodyDataContractSerializer(
            Type remotingRequestType,
            IEnumerable<Type> knownTypes)
        {
            var serializer = new DataContractSerializer(
                remotingRequestType,
                new DataContractSerializerSettings()
                {
                    MaxItemsInObjectGraph = int.MaxValue,
                    KnownTypes = AddDefaultKnownTypes(knownTypes),
                });

            serializer.SetSerializationSurrogateProvider(new ActorDataContractSurrogate());
            return serializer;
        }

        private static IEnumerable<Type> AddDefaultKnownTypes(IEnumerable<Type> knownTypes)
        {
            var types = new List<Type>(knownTypes);
            types.AddRange(DefaultKnownTypes);
            return types;
        }

        /// <summary>
        ///     Default serializer for service remoting request and response message body that uses the
        ///     memory stream to create outgoing message buffers.
        /// </summary>
        private class MemoryStreamMessageBodySerializer :
            IActorMessageBodySerializer
        {
            private readonly ActorMessageBodyDataContractSerializationProvider serializationProvider;
            private readonly DataContractSerializer serializer;

            public MemoryStreamMessageBodySerializer(
                ActorMessageBodyDataContractSerializationProvider serializationProvider,
                DataContractSerializer serializer)
            {
                this.serializationProvider = serializationProvider;
                this.serializer = serializer;
            }

            byte[] IActorMessageBodySerializer.Serialize(
                IActorMessageBody serviceRemotingRequestMessageBody)
            {
                if (serviceRemotingRequestMessageBody == null)
                {
                    return null;
                }

                using (var stream = new MemoryStream())
                {
                    using (var writer = this.CreateXmlDictionaryWriter(stream))
                    {
                        this.serializer.WriteObject(writer, serviceRemotingRequestMessageBody);
                        writer.Flush();

                        return stream.ToArray();
                    }
                }
            }

            IActorMessageBody IActorMessageBodySerializer.Deserialize(
                Stream messageBody)
            {
                if (messageBody == null || messageBody.Length == 0)
                {
                    return null;
                }

                using (var stream = new DisposableStream(messageBody))
                {
                    using (var reader = this.CreateXmlDictionaryReader(stream))
                    {
                        return (WrappedMessageBody)this.serializer.ReadObject(reader);
                    }
                }
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
            private XmlDictionaryWriter CreateXmlDictionaryWriter(Stream outputStream)
            {
                return this.serializationProvider.CreateXmlDictionaryWriter(outputStream);
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
            private XmlDictionaryReader CreateXmlDictionaryReader(Stream inputStream)
            {
                return this.serializationProvider.CreateXmlDictionaryReader(inputStream);
            }
        }
    }
}
