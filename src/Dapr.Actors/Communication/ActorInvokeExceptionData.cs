// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;
    using Dapr.Actors;
    using Microsoft.Extensions.Logging;

    [DataContract(Name = "ActorInvokeExceptionData", Namespace = Constants.Namespace)]
    internal class ActorInvokeExceptionData
    {
        private static readonly DataContractSerializer ActorInvokeExceptionDataSerializer = new DataContractSerializer(typeof(ActorInvokeExceptionData));

        public ActorInvokeExceptionData(string type, string message)
        {
            this.Type = type;
            this.Message = message;
        }

        [DataMember]
        public string Type { get; private set; }

        [DataMember]
        public string Message { get; private set; }

        internal static ActorInvokeExceptionData Deserialize(Stream stream)
        {
            if ((stream == null) || (stream.Length == 0))
            {
                return null;
            }

            using var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
            return (ActorInvokeExceptionData)ActorInvokeExceptionDataSerializer.ReadObject(reader);
        }

        internal byte[] Serialize()
        {
            using var stream = new MemoryStream();
            using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
            ActorInvokeExceptionDataSerializer.WriteObject(writer, this);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
