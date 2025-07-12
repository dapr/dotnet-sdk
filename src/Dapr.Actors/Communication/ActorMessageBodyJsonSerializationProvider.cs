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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// This is the implmentation  for <see cref="IActorMessageBodySerializationProvider"/>used by remoting service and client during
/// request/response serialization . It uses request Wrapping and data contract for serialization.
/// </summary>
internal class ActorMessageBodyJsonSerializationProvider : IActorMessageBodySerializationProvider
{
    public JsonSerializerOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorMessageBodyJsonSerializationProvider"/> class.
    /// </summary>
    public ActorMessageBodyJsonSerializationProvider(JsonSerializerOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Creates a MessageFactory for Wrapped Message Json Remoting Types. This is used to create Remoting Request/Response objects.
    /// </summary>
    /// <returns>
    /// <see cref="IActorMessageBodyFactory" /> that provides an instance of the factory for creating
    /// remoting request and response message bodies.
    /// </returns>
    public IActorMessageBodyFactory CreateMessageBodyFactory()
    {
        return new WrappedRequestMessageFactory();
    }

    /// <summary>
    /// Creates IActorRequestMessageBodySerializer for a serviceInterface using Wrapped Message Json implementation.
    /// </summary>
    /// <param name="serviceInterfaceType">The remoted service interface.</param>
    /// <param name="methodRequestParameterTypes">The union of parameter types of all of the methods of the specified interface.</param>
    /// <param name="wrappedRequestMessageTypes">Wrapped Request Types for all Methods.</param>
    /// <returns>
    /// An instance of the <see cref="IActorRequestMessageBodySerializer" /> that can serialize the service
    /// actor request message body to a messaging body for transferring over the transport.
    /// </returns>
    public IActorRequestMessageBodySerializer CreateRequestMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodRequestParameterTypes,
        IEnumerable<Type> wrappedRequestMessageTypes = null)
    {
        return new MemoryStreamMessageBodySerializer<WrappedMessageBody, WrappedMessageBody>(Options, serviceInterfaceType, methodRequestParameterTypes, wrappedRequestMessageTypes);
    }

    /// <summary>
    /// Creates IActorResponseMessageBodySerializer for a serviceInterface using Wrapped Message Json implementation.
    /// </summary>
    /// <param name="serviceInterfaceType">The remoted service interface.</param>
    /// <param name="methodReturnTypes">The return types of all of the methods of the specified interface.</param>
    /// <param name="wrappedResponseMessageTypes">Wrapped Response Types for all remoting methods.</param>
    /// <returns>
    /// An instance of the <see cref="IActorResponseMessageBodySerializer" /> that can serialize the service
    /// actor response message body to a messaging body for transferring over the transport.
    /// </returns>
    public IActorResponseMessageBodySerializer CreateResponseMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodReturnTypes,
        IEnumerable<Type> wrappedResponseMessageTypes = null)
    {
        return new MemoryStreamMessageBodySerializer<WrappedMessageBody, WrappedMessageBody>(Options, serviceInterfaceType, methodReturnTypes, wrappedResponseMessageTypes);
    }

    /// <summary>
    ///     Default serializer for service remoting request and response message body that uses the
    ///     memory stream to create outgoing message buffers.
    /// </summary>
    private class MemoryStreamMessageBodySerializer<TRequest, TResponse> :
        IActorRequestMessageBodySerializer,
        IActorResponseMessageBodySerializer
        where TRequest : IActorRequestMessageBody
        where TResponse : IActorResponseMessageBody
    {
        private readonly JsonSerializerOptions serializerOptions;

        public MemoryStreamMessageBodySerializer(
            JsonSerializerOptions serializerOptions,
            Type serviceInterfaceType,
            IEnumerable<Type> methodRequestParameterTypes,
            IEnumerable<Type> wrappedRequestMessageTypes = null)
        {
            var _methodRequestParameterTypes = new List<Type>(methodRequestParameterTypes);
            var _wrappedRequestMessageTypes = new List<Type>(wrappedRequestMessageTypes);
            if(_wrappedRequestMessageTypes.Count > 1){
                throw new NotSupportedException("JSON serialisation should always provide the actor method (or nothing), that was called" +
                                                " to support (de)serialisation. This is a Dapr SDK error, open an issue on GitHub.");
            }
            this.serializerOptions = new(serializerOptions)
            {
                // Workaround since WrappedMessageBody creates an object
                // with parameters as fields
                IncludeFields = true,
            };

            this.serializerOptions.Converters.Add(new ActorMessageBodyJsonConverter<TRequest>(_methodRequestParameterTypes, _wrappedRequestMessageTypes));
            this.serializerOptions.Converters.Add(new ActorMessageBodyJsonConverter<TResponse>(_methodRequestParameterTypes, _wrappedRequestMessageTypes));
        }

        byte[] IActorRequestMessageBodySerializer.Serialize(IActorRequestMessageBody actorRequestMessageBody)
        {
            if (actorRequestMessageBody == null)
            {
                return null;
            }

            return JsonSerializer.SerializeToUtf8Bytes<object>(actorRequestMessageBody, this.serializerOptions);
        }

        async ValueTask<IActorRequestMessageBody> IActorRequestMessageBodySerializer.DeserializeAsync(Stream stream)
        {
            if (stream == null)
            {
                return default;
            }

            if (stream.Length == 0)
            {
                return default;
            }

            stream.Position = 0;
            return await JsonSerializer.DeserializeAsync<TRequest>(stream, this.serializerOptions);
        }

        byte[] IActorResponseMessageBodySerializer.Serialize(IActorResponseMessageBody actorResponseMessageBody)
        {
            if (actorResponseMessageBody == null)
            {
                return null;
            }

            return JsonSerializer.SerializeToUtf8Bytes<object>(actorResponseMessageBody, this.serializerOptions);
        }

        async ValueTask<IActorResponseMessageBody> IActorResponseMessageBodySerializer.DeserializeAsync(Stream messageBody)
        {
            if (messageBody == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            messageBody.CopyTo(stream);
            stream.Position = 0;

            if (stream.Capacity == 0)
            {
                return null;
            }

            return await JsonSerializer.DeserializeAsync<TResponse>(stream, this.serializerOptions);
        }
    }
}