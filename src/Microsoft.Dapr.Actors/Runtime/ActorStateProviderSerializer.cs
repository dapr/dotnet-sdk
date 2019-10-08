// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    internal class ActorStateProviderSerializer
    {
        private readonly ConcurrentDictionary<Type, DataContractSerializer> actorStateSerializerCache;

        internal ActorStateProviderSerializer()
        {
            this.actorStateSerializerCache = new ConcurrentDictionary<Type, DataContractSerializer>();
        }

        internal byte[] Serialize<T>(Type stateType, T state)
        {
            var serializer = this.actorStateSerializerCache.GetOrAdd(
                stateType,
                CreateDataContractSerializer);

            using var stream = new MemoryStream();
            using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
            serializer.WriteObject(writer, state);
            writer.Flush();
            return stream.ToArray();
        }

        internal T Deserialize<T>(byte[] buffer)
        {
            if ((buffer == null) || (buffer.Length == 0))
            {
                return default;
            }

            var serializer = this.actorStateSerializerCache.GetOrAdd(
                typeof(T),
                CreateDataContractSerializer);

            using var stream = new MemoryStream(buffer);
            using var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
            return (T)serializer.ReadObject(reader);
        }

        private static DataContractSerializer CreateDataContractSerializer(Type actorStateType)
        {
            var dataContractSerializer = new DataContractSerializer(
                actorStateType,
                new DataContractSerializerSettings
                {
                    MaxItemsInObjectGraph = int.MaxValue,
                    KnownTypes = new[]
                    {
                        typeof(ActorReference),
                    },
                });

            return dataContractSerializer;
        }
    }
}
