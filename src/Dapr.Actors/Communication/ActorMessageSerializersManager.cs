// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System;
    using System.Collections.Concurrent;
    using Dapr.Actors.Builder;

    /// <summary>
    /// ActorMessageSerializersManager interface
    /// </summary>
    public class ActorMessageSerializersManager
    {
        private readonly ConcurrentDictionary<int, CacheEntry> cachedBodySerializers;
        private readonly IActorMessageHeaderSerializer headerSerializer;
        private readonly IActorMessageBodySerializationProvider serializationProvider;

        /// <summary>
        /// The constructor
        /// </summary>
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

        /// <summary>
        /// Gets the serialization provider
        /// </summary>
        public IActorMessageBodySerializationProvider GetSerializationProvider()
        {
            return this.serializationProvider;
        }

        /// <summary>
        /// Gets the header serializer
        /// </summary>
        public IActorMessageHeaderSerializer GetHeaderSerializer()
        {
            return this.headerSerializer;
        }

        /// <summary>
        /// Gets the request message body serializer
        /// </summary>
        public IActorRequestMessageBodySerializer GetRequestMessageBodySerializer(int interfaceId)
        {
            return this.cachedBodySerializers.GetOrAdd(interfaceId, this.CreateSerializers).RequestMessageBodySerializer;
        }

        /// <summary>
        /// Gets the response message body serializer
        /// </summary>
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
