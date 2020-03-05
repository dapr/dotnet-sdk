﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Autogenerated = Dapr.Client.Autogen.Grpc;

    /// <summary>
    /// A client for interacting with the Dapr endpoints.
    /// </summary>
    public class DaprClient : IDaprClient
    {
        private readonly Autogenerated.Dapr.DaprClient client;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClient"/> class.
        /// </summary>
        /// <param name="channel">gRPC channel to create gRPC clients.</param>
        /// <param name="jsonSerializerOptions">Json serialization options.</param>
        internal DaprClient(GrpcChannel channel, JsonSerializerOptions jsonSerializerOptions = null)
        {
            this.jsonSerializerOptions = jsonSerializerOptions;
            this.client = new Autogenerated.Dapr.DaprClient(channel);
        }

        /// <inheritdoc/>
        public override Task PublishEventAsync<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
            {
                throw new ArgumentException("The value cannot be null or empty", nameof(topicName));
            }

            if (publishContent is null)
            {
                throw new ArgumentNullException(nameof(publishContent));
            }

            return MakePublishRequest(topicName, publishContent, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
            {
                throw new ArgumentException("The value cannot be null or empty", nameof(topicName));
            }

            return MakePublishRequest(topicName, string.Empty, cancellationToken);
        }

        private async Task MakePublishRequest<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken)
        {
            // Create PublishEventEnvelope
            var eventToPublish = new Autogenerated.PublishEventEnvelope()
            {
                Topic = topicName,
            };

            if (publishContent != null)
            {
                using var stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, publishContent, this.jsonSerializerOptions, cancellationToken);
                await stream.FlushAsync();

                // set the position to beginning of stream.
                stream.Seek(0, SeekOrigin.Begin);

                var data = new Any
                {
                    Value = await ByteString.FromStreamAsync(stream)
                };

                eventToPublish.Data = data;                
            }

            var callOptions = new CallOptions(cancellationToken: cancellationToken);
            await client.PublishEventAsync(eventToPublish, callOptions);
        }
    }
}
