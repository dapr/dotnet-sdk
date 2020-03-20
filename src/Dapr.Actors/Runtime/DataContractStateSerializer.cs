// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Xml;

    /// <summary>
    /// DataContract serializer for Actor state serialization/deserialziation.
    /// This is the default state serializer used with Service Fabric Reliable Actors.
    /// If there is user ask for the compatibility, this can be exposed by adding a compatibility option as an attribute on Actor type so that Service Fabric Reliable Actors state serialization behavior
    /// can also be used using Dapr.
    /// </summary>
    internal class DataContractStateSerializer : IActorStateSerializer
    {
        private readonly ConcurrentDictionary<Type, DataContractSerializer> actorStateSerializerCache;

        internal DataContractStateSerializer()
        {
            this.actorStateSerializerCache = new ConcurrentDictionary<Type, DataContractSerializer>();
        }

        public byte[] Serialize<T>(Type stateType, T state)
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

        public T Deserialize<T>(byte[] buffer)
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
