// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Communication;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;

/// <summary>
/// This is the implmentation  for <see cref="IActorMessageBodySerializationProvider"/>used by remoting service and client during
/// request/response serialization . It uses request Wrapping and data contract for serialization.
/// </summary>
internal class ActorMessageBodyDataContractSerializationProvider : IActorMessageBodySerializationProvider
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
    /// Creates IActorRequestMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
    /// </summary>
    /// <param name="serviceInterfaceType">The remoted service interface.</param>
    /// <param name="methodRequestParameterTypes">The union of parameter types of all of the methods of the specified interface.</param>
    /// <param name="wrappedRequestMessageTypes">Wrapped Request Types for all Methods.</param>
    /// <returns>
    /// An instance of the <see cref="IActorRequestMessageBodySerializer" /> that can serialize the service
    /// actor request message body to a messaging body for transferring over the transport.
    /// </returns>
    public IActorRequestMessageBodySerializer CreateRequestMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodRequestParameterTypes,
        IEnumerable<Type> wrappedRequestMessageTypes = null)
    {
        var knownTypes = new List<Type>(DefaultKnownTypes);
        knownTypes.AddRange(wrappedRequestMessageTypes);

        DataContractSerializer serializer = this.CreateMessageBodyDataContractSerializer(
            typeof(WrappedMessageBody),
            knownTypes);

        return new MemoryStreamMessageBodySerializer<WrappedMessageBody, WrappedMessageBody>(this, serializer);
    }

    /// <summary>
    /// Creates IActorResponseMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
    /// </summary>
    /// <param name="serviceInterfaceType">The remoted service interface.</param>
    /// <param name="methodReturnTypes">The return types of all of the methods of the specified interface.</param>
    /// <param name="wrappedResponseMessageTypes">Wrapped Response Types for all remoting methods.</param>
    /// <returns>
    /// An instance of the <see cref="IActorResponseMessageBodySerializer" /> that can serialize the service
    /// actor response message body to a messaging body for transferring over the transport.
    /// </returns>
    public IActorResponseMessageBodySerializer CreateResponseMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodReturnTypes,
        IEnumerable<Type> wrappedResponseMessageTypes = null)
    {
        var knownTypes = new List<Type>(DefaultKnownTypes);
        knownTypes.AddRange(wrappedResponseMessageTypes);

        DataContractSerializer serializer = this.CreateMessageBodyDataContractSerializer(
            typeof(WrappedMessageBody),
            knownTypes);

        return new MemoryStreamMessageBodySerializer<WrappedMessageBody, WrappedMessageBody>(this, serializer);
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
                KnownTypes = knownTypes,
            });

        serializer.SetSerializationSurrogateProvider(new ActorDataContractSurrogate());
        return serializer;
    }

    /// <summary>
    ///     Default serializer for service remoting request and response message body that uses the
    ///     memory stream to create outgoing message buffers.
    /// </summary>
    private class MemoryStreamMessageBodySerializer<TRequest, TResponse> :
        IActorRequestMessageBodySerializer,
        IActorResponseMessageBodySerializer
        where TRequest : IActorRequestMessageBody
        where TResponse : IActorResponseMessageBody
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

        byte[] IActorRequestMessageBodySerializer.Serialize(IActorRequestMessageBody actorRequestMessageBody)
        {
            if (actorRequestMessageBody == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            using var writer = this.CreateXmlDictionaryWriter(stream);
            this.serializer.WriteObject(writer, actorRequestMessageBody);
            writer.Flush();

            return stream.ToArray();
        }

        ValueTask<IActorRequestMessageBody> IActorRequestMessageBodySerializer.DeserializeAsync(Stream stream)
        {
            if (stream == null)
            {
                return default;
            }

            if (stream.Length == 0)
            {
                return default;
            }

            stream.Position = 0;
            using var reader = this.CreateXmlDictionaryReader(stream);
            return new ValueTask<IActorRequestMessageBody>((TRequest)this.serializer.ReadObject(reader));
        }

        byte[] IActorResponseMessageBodySerializer.Serialize(IActorResponseMessageBody actorResponseMessageBody)
        {
            if (actorResponseMessageBody == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            using var writer = this.CreateXmlDictionaryWriter(stream);
            this.serializer.WriteObject(writer, actorResponseMessageBody);
            writer.Flush();

            return stream.ToArray();
        }

        ValueTask<IActorResponseMessageBody> IActorResponseMessageBodySerializer.DeserializeAsync(Stream messageBody)
        {
            if (messageBody == null)
            {
                return default;
            }

            // TODO check performance
            using var stream = new MemoryStream();
            messageBody.CopyTo(stream);
            stream.Position = 0;

            if (stream.Capacity == 0)
            {
                return default;
            }

            using var reader = this.CreateXmlDictionaryReader(stream);
            return new ValueTask<IActorResponseMessageBody>((TResponse)this.serializer.ReadObject(reader));
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