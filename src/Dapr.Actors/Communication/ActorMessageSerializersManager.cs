// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System;
    using System.Collections.Concurrent;
    using Dapr.Actors.Builder;

    internal class ActorMessageSerializersManager
    {
        private readonly ConcurrentDictionary<int, CacheEntry> cachedBodySerializers;
        private readonly IActorMessageHeaderSerializer headerSerializer;
        private readonly IActorMessageBodySerializationProvider serializationProvider;

        public ActorMessageSerializersManager(
            IActorMessageBodySerializationProvider serializationProvider,
            IActorMessageHeaderSerializer headerSerializer)
        {
            if (serializationProvider == null)
            {
                serializationProvider = new ActorMessageBodyDataContractSerializationProvider();
            }

            if (headerSerializer == null)
            {
                headerSerializer = new ActorMessageHeaderSerializer();
            }

            this.serializationProvider = serializationProvider;
            this.cachedBodySerializers = new ConcurrentDictionary<int, CacheEntry>();
            this.headerSerializer = headerSerializer;
        }

        public IActorMessageBodySerializationProvider GetSerializationProvider()
        {
            return this.serializationProvider;
        }

        public IActorMessageHeaderSerializer GetHeaderSerializer()
        {
            return this.headerSerializer;
        }

        public IActorRequestMessageBodySerializer GetRequestMessageBodySerializer(int interfaceId)
        {
            return this.cachedBodySerializers.GetOrAdd(interfaceId, this.CreateSerializers).RequestMessageBodySerializer;
        }

        public IActorResponseMessageBodySerializer GetResponseMessageBodySerializer(int interfaceId)
        {
            return this.cachedBodySerializers.GetOrAdd(interfaceId, this.CreateSerializers).ResponseMessageBodySerializer;
        }

        internal CacheEntry CreateSerializers(int interfaceId)
        {
            var interfaceDetails = this.GetInterfaceDetails(interfaceId);

            // get the service interface type from the code gen layer
            var serviceInterfaceType = interfaceDetails.ServiceInterfaceType;

            // get the known types from the codegen layer
            var requestBodyTypes = interfaceDetails.RequestKnownTypes;

            // get the known types from the codegen layer
            var responseBodyTypes = interfaceDetails.ResponseKnownTypes;

            return new CacheEntry(
               this.serializationProvider.CreateRequestMessageBodySerializer(serviceInterfaceType, requestBodyTypes, interfaceDetails.RequestWrappedKnownTypes),
               this.serializationProvider.CreateResponseMessageBodySerializer(serviceInterfaceType, responseBodyTypes, interfaceDetails.ResponseWrappedKnownTypes));
        }

        internal InterfaceDetails GetInterfaceDetails(int interfaceId)
        {
            if (!ActorCodeBuilder.TryGetKnownTypes(interfaceId, out var interfaceDetails))
            {
                throw new ArgumentException("No interface found with this Id  " + interfaceId);
            }

            return interfaceDetails;
        }
    }
}
