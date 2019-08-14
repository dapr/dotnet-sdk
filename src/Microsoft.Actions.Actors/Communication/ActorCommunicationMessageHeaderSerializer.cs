// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    internal class ActorCommunicationMessageHeaderSerializer : IActorCommunicationMessageHeaderSerializer
    {
        private readonly DataContractSerializer requestHeaderSerializer;
        private readonly DataContractSerializer responseHeaderSerializer;

        public ActorCommunicationMessageHeaderSerializer()
            : this(
                new DataContractSerializer(
                    typeof(IRequestMessageHeader),
                    new DataContractSerializerSettings()
                    {
                        MaxItemsInObjectGraph = int.MaxValue,
                        KnownTypes = new[] 
                        {
                            typeof(RequestMessageHeader),
                        },
                    }))
        {
        }

        public ActorCommunicationMessageHeaderSerializer(
            DataContractSerializer headerRequestSerializer)
        {
            this.requestHeaderSerializer = headerRequestSerializer;
            this.responseHeaderSerializer = new DataContractSerializer(
                typeof(IResponseMessageHeader),
                new DataContractSerializerSettings()
                {
                    MaxItemsInObjectGraph = int.MaxValue,
                    KnownTypes = new[] { typeof(ResponseMessageHeader) },
                });
        }

        public IMessageHeader SerializeRequestHeader(IRequestMessageHeader serviceRemotingRequestMessageHeader)
        {
            if (serviceRemotingRequestMessageHeader == null)
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
                {
                    this.requestHeaderSerializer.WriteObject(writer, serviceRemotingRequestMessageHeader);
                    writer.Flush();
                    return new OutgoingMessageHeader(stream.ToArray());
                }
            }
        }

        public IRequestMessageHeader DeserializeRequestHeaders(IMessageHeader messageHeader)
        {
            if ((messageHeader == null) || (messageHeader.GetSendBytes() == null) ||
                (messageHeader.GetSendBytes().Length == 0))
            {
                return null;
            }

            using (var reader = XmlDictionaryReader.CreateBinaryReader(
                messageHeader.GetSendBytes(),
                XmlDictionaryReaderQuotas.Max))
            {
                return (IRequestMessageHeader)this.requestHeaderSerializer.ReadObject(reader);
            }
        }

        public IMessageHeader SerializeResponseHeader(IResponseMessageHeader serviceRemotingResponseMessageHeader)
        {
            if (serviceRemotingResponseMessageHeader == null || serviceRemotingResponseMessageHeader.CheckIfItsEmpty())
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
                {
                    this.responseHeaderSerializer.WriteObject(writer, serviceRemotingResponseMessageHeader);
                    writer.Flush();
                    return new OutgoingMessageHeader(stream.ToArray());
                }
            }
        }

        public IResponseMessageHeader DeserializeResponseHeaders(IMessageHeader messageHeader)
        {
            if ((messageHeader == null) || (messageHeader.GetSendBytes() == null) ||
                (messageHeader.GetSendBytes().Length == 0))
            {
                return null;
            }

            using (var reader = XmlDictionaryReader.CreateBinaryReader(
                messageHeader.GetSendBytes(),
                XmlDictionaryReaderQuotas.Max))
            {
                return (IResponseMessageHeader)this.responseHeaderSerializer.ReadObject(reader);
            }
        }
    }
}
