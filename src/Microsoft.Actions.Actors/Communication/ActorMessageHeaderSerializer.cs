// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    internal class ActorMessageHeaderSerializer : IActorMessageHeaderSerializer
    {
        private readonly DataContractSerializer requestHeaderSerializer;
        private readonly DataContractSerializer responseHeaderSerializer;

        public ActorMessageHeaderSerializer()
            : this(
                new DataContractSerializer(
                    typeof(IActorRequestMessageHeader),
                    new DataContractSerializerSettings()
                    {
                        MaxItemsInObjectGraph = int.MaxValue,
                        KnownTypes = new[] 
                        {
                            typeof(ActorRequestMessageHeader),
                        },
                    }))
        {
        }

        public ActorMessageHeaderSerializer(
            DataContractSerializer headerRequestSerializer)
        {
            this.requestHeaderSerializer = headerRequestSerializer;
            this.responseHeaderSerializer = new DataContractSerializer(
                typeof(IActorResponseMessageHeader),
                new DataContractSerializerSettings()
                {
                    MaxItemsInObjectGraph = int.MaxValue,
                    KnownTypes = new[] { typeof(ActorResponseMessageHeader) },
                });
        }

        public IMessageHeader SerializeRequestHeader(IActorRequestMessageHeader serviceRemotingRequestMessageHeader)
        {
            if (serviceRemotingRequestMessageHeader == null)
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = XmlDictionaryWriter.CreateTextWriter(stream))
                {
                    this.requestHeaderSerializer.WriteObject(writer, serviceRemotingRequestMessageHeader);
                    writer.Flush();
                    return new OutgoingMessageHeader(stream.ToArray());
                }
            }
        }

        public IActorRequestMessageHeader DeserializeRequestHeaders(IMessageHeader messageHeader)
        {
            if ((messageHeader == null) || (messageHeader.GetReceivedBuffer() == null) ||
                (messageHeader.GetReceivedBuffer().Length == 0))
            {
                return null;
            }

            using (var reader = XmlDictionaryReader.CreateTextReader(
                messageHeader.GetSendBytes(),
                XmlDictionaryReaderQuotas.Max))
            {
                return (IActorRequestMessageHeader)this.requestHeaderSerializer.ReadObject(reader);
            }
        }

        public IMessageHeader SerializeResponseHeader(IActorResponseMessageHeader serviceRemotingResponseMessageHeader)
        {
            if (serviceRemotingResponseMessageHeader == null || serviceRemotingResponseMessageHeader.CheckIfItsEmpty())
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = XmlDictionaryWriter.CreateTextWriter(stream))
                {
                    this.responseHeaderSerializer.WriteObject(writer, serviceRemotingResponseMessageHeader);
                    writer.Flush();
                    return new OutgoingMessageHeader(stream.ToArray());
                }
            }
        }

        public IActorResponseMessageHeader DeserializeResponseHeaders(IMessageHeader messageHeader)
        {
            if ((messageHeader == null) || (messageHeader.GetReceivedBuffer() == null) ||
                (messageHeader.GetReceivedBuffer().Length == 0))
            {
                return null;
            }

            using (var reader = XmlDictionaryReader.CreateTextReader(
                messageHeader.GetSendBytes(),
                XmlDictionaryReaderQuotas.Max))
            {
                return (IActorResponseMessageHeader)this.responseHeaderSerializer.ReadObject(reader);
            }
        }
    }
}
