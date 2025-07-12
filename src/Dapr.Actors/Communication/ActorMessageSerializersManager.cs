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

namespace Dapr.Actors.Communication;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapr.Actors.Builder;

internal class ActorMessageSerializersManager
{
    private readonly ConcurrentDictionary<(int, string), CacheEntry> cachedBodySerializers;
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
        this.cachedBodySerializers = new ConcurrentDictionary<(int, string), CacheEntry>();
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

    public IActorRequestMessageBodySerializer GetRequestMessageBodySerializer(int interfaceId, [AllowNull] string methodName = null)
    {
        return this.cachedBodySerializers.GetOrAdd((interfaceId, methodName), this.CreateSerializers).RequestMessageBodySerializer;
    }

    public IActorResponseMessageBodySerializer GetResponseMessageBodySerializer(int interfaceId, [AllowNull] string methodName = null)
    {
        return this.cachedBodySerializers.GetOrAdd((interfaceId, methodName), this.CreateSerializers).ResponseMessageBodySerializer;
    }

    internal CacheEntry CreateSerializers((int interfaceId, string methodName) data)
    {
        var interfaceDetails = this.GetInterfaceDetails(data.interfaceId);

        // get the service interface type from the code gen layer
        var serviceInterfaceType = interfaceDetails.ServiceInterfaceType;

        // get the known types from the codegen layer
        var requestBodyTypes = interfaceDetails.RequestKnownTypes;

        // get the known types from the codegen layer
        var responseBodyTypes = interfaceDetails.ResponseKnownTypes;
        if (data.methodName is null)
        {
            // Path is mainly used for XML serialization
            return new CacheEntry(
                this.serializationProvider.CreateRequestMessageBodySerializer(serviceInterfaceType, requestBodyTypes, interfaceDetails.RequestWrappedKnownTypes),
                this.serializationProvider.CreateResponseMessageBodySerializer(serviceInterfaceType, responseBodyTypes, interfaceDetails.ResponseWrappedKnownTypes));
        }
        else
        {
            // This path should be used for JSON serialization
            var requestWrapperTypeAsList = interfaceDetails.RequestWrappedKnownTypes.Where(r => r.Name == $"{data.methodName}ReqBody").ToList();
            if(requestWrapperTypeAsList.Count > 1){
                throw new NotSupportedException($"More then one wrappertype was found for {data.methodName}");
            }
            var responseWrapperTypeAsList = interfaceDetails.ResponseWrappedKnownTypes.Where(r => r.Name == $"{data.methodName}RespBody").ToList();
            if(responseWrapperTypeAsList.Count > 1){
                throw new NotSupportedException($"More then one wrappertype was found for {data.methodName}");
            }
            return new CacheEntry(
                this.serializationProvider.CreateRequestMessageBodySerializer(serviceInterfaceType, requestBodyTypes, requestWrapperTypeAsList),
                this.serializationProvider.CreateResponseMessageBodySerializer(serviceInterfaceType, responseBodyTypes, responseWrapperTypeAsList));
        }

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