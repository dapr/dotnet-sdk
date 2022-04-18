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
