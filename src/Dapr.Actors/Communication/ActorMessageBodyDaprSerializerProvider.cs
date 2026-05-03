// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Common.Serialization;

/// <summary>
/// An <see cref="IActorMessageBodySerializationProvider"/> implementation that uses <see cref="IDaprSerializer"/>
/// for serialization of actor remoting request and response message bodies.
/// </summary>
internal class ActorMessageBodyDaprSerializerProvider : IActorMessageBodySerializationProvider
{
    private readonly IDaprSerializer serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorMessageBodyDaprSerializerProvider"/> class.
    /// </summary>
    /// <param name="serializer">The <see cref="IDaprSerializer"/> instance to use for serialization.</param>
    public ActorMessageBodyDaprSerializerProvider(IDaprSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc/>
    public IActorMessageBodyFactory CreateMessageBodyFactory()
    {
        return new WrappedRequestMessageFactory();
    }

    /// <inheritdoc/>
    public IActorRequestMessageBodySerializer CreateRequestMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodRequestParameterTypes,
        IEnumerable<Type> wrappedRequestMessageTypes = null)
    {
        return new DaprSerializerMessageBodySerializer(this.serializer);
    }

    /// <inheritdoc/>
    public IActorResponseMessageBodySerializer CreateResponseMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodReturnTypes,
        IEnumerable<Type> wrappedResponseMessageTypes = null)
    {
        return new DaprSerializerMessageBodySerializer(this.serializer);
    }

    /// <summary>
    /// Serializer that delegates to <see cref="IDaprSerializer"/> for actor message body serialization.
    /// </summary>
    private sealed class DaprSerializerMessageBodySerializer :
        IActorRequestMessageBodySerializer,
        IActorResponseMessageBodySerializer
    {
        private readonly IDaprSerializer serializer;

        public DaprSerializerMessageBodySerializer(IDaprSerializer serializer)
        {
            this.serializer = serializer;
        }

        byte[] IActorRequestMessageBodySerializer.Serialize(IActorRequestMessageBody actorRequestMessageBody)
        {
            if (actorRequestMessageBody == null)
            {
                return null;
            }

            var json = this.serializer.Serialize(actorRequestMessageBody);
            return Encoding.UTF8.GetBytes(json);
        }

        async ValueTask<IActorRequestMessageBody> IActorRequestMessageBodySerializer.DeserializeAsync(Stream stream)
        {
            if (stream == null || stream.Length == 0)
            {
                return default;
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var json = await reader.ReadToEndAsync();
            return this.serializer.Deserialize<WrappedMessageBody>(json);
        }

        byte[] IActorResponseMessageBodySerializer.Serialize(IActorResponseMessageBody actorResponseMessageBody)
        {
            if (actorResponseMessageBody == null)
            {
                return null;
            }

            var json = this.serializer.Serialize(actorResponseMessageBody);
            return Encoding.UTF8.GetBytes(json);
        }

        async ValueTask<IActorResponseMessageBody> IActorResponseMessageBodySerializer.DeserializeAsync(Stream messageBody)
        {
            if (messageBody == null)
            {
                return null;
            }

            using var stream = new MemoryStream();
            messageBody.CopyTo(stream);

            if (stream.Length == 0)
            {
                return null;
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            return this.serializer.Deserialize<WrappedMessageBody>(json);
        }
    }
}
