// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using C = Dapr.AppCallback.Autogen.Grpc.v1;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.PublishSubscribe;

sealed class DaprPublishSubscribeGrpcClient : DaprPublishSubscribeClient
{
    private readonly P.Dapr.DaprClient client;

    public DaprPublishSubscribeGrpcClient(GrpcChannel channel)
    {
        this.client = new P.Dapr.DaprClient(channel);
    }

    public override async Task SubscribeAsync(string pubSubName, string topicName, TopicRequestHandler handler, DaprSubscriptionOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = this.client.SubscribeTopicEventsAlpha1(cancellationToken: cancellationToken);

            P.SubscribeTopicEventsInitialRequestAlpha1 initialRequest =
                new()
                {
                    PubsubName = pubSubName,
                    Topic = topicName,
                    DeadLetterTopic = options?.DeadLetterTopic ?? String.Empty
                };

            if (options?.Metadata.Count > 0)
            {
                foreach (var (key, value) in options.Metadata)
                {
                    initialRequest.Metadata.Add(key, value);
                }
            }

            await result.RequestStream.WriteAsync(
                new()
                {
                    InitialRequest = initialRequest
                },
                cancellationToken);
            
            await foreach (var response in result.ResponseStream.ReadAllAsync(cancellationToken))
            {
                var request = new TopicRequest
                {
                    Id = response.Id,
                    Source = response.Source,
                    Type = response.Type,
                    SpecVersion = response.SpecVersion,
                    DataContentType = response.DataContentType,
                    Topic = response.Topic,
                    PubSubName = response.PubsubName,
                    Path = response.Path,
                    Extensions = response.Extensions
                };

                var topicResponse = await handler(request, cancellationToken);

                await result.RequestStream.WriteAsync(
                    new()
                    {
                        EventResponse =
                            new()
                            {
                                Id = response.Id,
                                Status =
                                    new()
                                    {
                                        Status = topicResponse switch
                                        {
                                            TopicResponse.Success => C.TopicEventResponse.Types.TopicEventResponseStatus.Success,
                                            TopicResponse.Retry => C.TopicEventResponse.Types.TopicEventResponseStatus.Retry,
                                            TopicResponse.Drop => C.TopicEventResponse.Types.TopicEventResponseStatus.Drop,
                                            _ => throw new InvalidOperationException("Unknown TopicResponse")
                                        }
                                    }
                            }
                    },
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ignore
        }
    }
}
