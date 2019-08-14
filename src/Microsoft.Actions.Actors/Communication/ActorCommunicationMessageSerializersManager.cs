// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Actions.Actors.Builder;

    internal class ActorCommunicationMessageSerializersManager
    {
        private readonly ConcurrentDictionary<int, CacheEntry> cachedBodySerializers;
        private readonly IActorCommunicationMessageHeaderSerializer headerSerializer;
        private readonly IActorCommunicationMessageSerializationProvider serializationProvider;

        public ActorCommunicationMessageSerializersManager(
            IActorCommunicationMessageSerializationProvider serializationProvider,
            IActorCommunicationMessageHeaderSerializer headerSerializer)
        {
            if (headerSerializer == null)
            {
                headerSerializer = new ActorCommunicationMessageHeaderSerializer();
            }

            if (serializationProvider == null)
            {
                serializationProvider = new ActorCommunicationWrappingDataContractSerializationProvider();
            }

            this.serializationProvider = serializationProvider;
            this.cachedBodySerializers = new ConcurrentDictionary<int, CacheEntry>();
            this.headerSerializer = headerSerializer;
        }

        public IActorCommunicationMessageSerializationProvider GetSerializationProvider()
        {
            return this.serializationProvider;
        }

        public IActorCommunicationMessageHeaderSerializer GetHeaderSerializer()
        {
            return this.headerSerializer;
        }

        public IActorCommunicationRequestMessageBodySerializer GetRequestBodySerializer(int interfaceId)
        {
            return this.cachedBodySerializers.GetOrAdd(interfaceId, this.CreateSerializers).RequestBodySerializer;
        }

        public IActorCommunicationResponseMessageBodySerializer GetResponseBodySerializer(int interfaceId)
        {
            return this.cachedBodySerializers.GetOrAdd(interfaceId, this.CreateSerializers).ResponseBodySerializer;
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
                this.serializationProvider.CreateRequestMessageSerializer(serviceInterfaceType, requestBodyTypes, interfaceDetails.RequestWrappedKnownTypes),
                this.serializationProvider.CreateResponseMessageSerializer(serviceInterfaceType, responseBodyTypes, interfaceDetails.ResponseWrappedKnownTypes));
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
