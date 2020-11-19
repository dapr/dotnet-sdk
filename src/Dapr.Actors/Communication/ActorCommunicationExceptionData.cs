// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;
    using Dapr.Actors;
    using Microsoft.Extensions.Logging;

    [DataContract(Name = "ActorCommunicationExceptionData", Namespace = Constants.Namespace)]
    internal class ActorCommunicationExceptionData
    {
        private static readonly DataContractSerializer ActorCommunicationExceptionDataSerializer = new DataContractSerializer(typeof(ActorCommunicationExceptionData));

        public ActorCommunicationExceptionData(string type, string message)
        {
            this.Type = type;
            this.Message = message;
        }

        [DataMember]
        public string Type { get; private set; }

        [DataMember]
        public string Message { get; private set; }

        internal static bool TryDeserialize(Stream data, out ActorCommunicationExceptionData result, ILogger logger = null)
        {
            try
            {
                var exceptionData = Deserialize(data);
                result = exceptionData;
                return true;
            }
            catch (Exception e)
            {
                // swallowing the exception
                logger?.LogWarning(
                    "ActorCommunicationException",
                    " ActorCommunicationExceptionData DeSerialization failed : Reason  {0}",
                    e);
            }

            result = null;
            return false;
        }

        internal static ActorCommunicationExceptionData Deserialize(Stream buffer)
        {
            if ((buffer == null) || (buffer.Length == 0))
            {
                return null;
            }

            using var reader = XmlDictionaryReader.CreateBinaryReader(buffer, XmlDictionaryReaderQuotas.Max);
            return (ActorCommunicationExceptionData)ActorCommunicationExceptionDataSerializer.ReadObject(reader);
        }

        internal byte[] Serialize()
        {
            using var stream = new MemoryStream();
            using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
            ActorCommunicationExceptionDataSerializer.WriteObject(writer, this);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
