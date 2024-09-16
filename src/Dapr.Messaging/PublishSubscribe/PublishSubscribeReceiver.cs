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

using System.Threading.Channels;
using Grpc.Core;
using C = Dapr.AppCallback.Autogen.Grpc.v1;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A thread-safe implementation of a receiver for messages from a specified Dapr publish/subscribe component and
/// topic.
/// </summary>
internal sealed class PublishSubscribeReceiver : IAsyncDisposable
{
    /// <summary>
    /// The name of the Dapr pubsub component.
    /// </summary>
    private readonly string pubSubName;
    /// <summary>
    /// The name of the topic to subscribe to.
    /// </summary>
    private readonly string topicName;
    /// <summary>
    /// Options allowing the behavior of the receiver to be configured.
    /// </summary>
    private readonly DaprSubscriptionOptions options;
    /// <summary>
    /// A channel used to decouple the messages received from the sidecar to their consumption.
    /// </summary>
    private readonly Channel<TopicMessage> channel = Channel.CreateUnbounded<TopicMessage>();
    /// <summary>
    /// The handler delegate responsible for processing the topic messages.
    /// </summary>
    private readonly TopicMessageHandler messageHandler;
    /// <summary>
    /// Maintains the connection to the Dapr dynamic subscription endpoint.
    /// </summary>
    private readonly ConnectionManager connectionManager;
    
    /// <summary>
    /// Constructs a new instance of a <see cref="PublishSubscribeReceiver"/> instance.
    /// </summary>
    /// <param name="pubSubName">The name of the Dapr Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Options allowing the behavior of the receiver to be configured.</param>
    /// <param name="connectionManager">Maintains the connection to the Dapr dynamic subscription endpoint.</param>
    /// <param name="handler">The delegate reflecting the action to take upon messages received by the subscription.</param>
    internal PublishSubscribeReceiver(string pubSubName, string topicName, DaprSubscriptionOptions options, ConnectionManager connectionManager, TopicMessageHandler handler)
    {
        this.pubSubName = pubSubName;
        this.topicName = topicName;
        this.options = options;
        this.connectionManager = connectionManager;
        this.messageHandler = handler;
    }

    /// <summary>
    /// Dynamically subscribes to messages on a PubSub topic provided by the Dapr sidecar.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TopicMessage}"/> containing messages provided by the sidecar.</returns>
    public async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var stream = await connectionManager.GetStreamAsync(cancellationToken);
        //Retrieve the messages from the sidecar and write to the channel
        _ = FetchDataFromSidecarAsync(stream, channel.Writer, cancellationToken);
        
        //Read the messages one-by-one out of the channel
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (channel.Reader.TryRead(out var message))
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(options.MessageHandlingPolicy.TimeoutDuration);

                    //Evaluate the message and return an acknowledgement result
                    var messageAction = await messageHandler(message, cts.Token);

                    try
                    {
                        //Share the result with the sidecar
                        await AcknowledgeMessageAsync(stream, message.Id, messageAction, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        //Acknowledge the message using the configured default response action
                        await AcknowledgeMessageAsync(stream, message.Id,
                            options.MessageHandlingPolicy.DefaultResponseAction, cts.Token);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            //Drain the remaining messages with the default action in the order in which they were queued
            while (channel.Reader.TryRead(out var message))
            {
                await AcknowledgeMessageAsync(stream, message.Id, options.MessageHandlingPolicy.DefaultResponseAction,
                    CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Acknowledges the indicated message back to the Dapr sidecar with an indicated behavior to take on the message.
    /// </summary>
    /// <param name="stream">The stream connection to and from the Dream sidecar instance.</param>
    /// <param name="messageId">The identifier of the message the behavior is in reference to.</param>
    /// <param name="action">The behavior to take on the message as indicated by either the message handler or timeout message handling configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    private static async Task AcknowledgeMessageAsync(AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest> stream, string messageId, TopicResponseAction action, CancellationToken cancellationToken)
    {
        await stream.RequestStream.WriteAsync(new P.SubscribeTopicEventsRequestAlpha1
        {
            EventResponse = new()
            {
                Id = messageId,
                Status = new()
                {
                    Status = action switch
                    {
                        TopicResponseAction.Success => C.TopicEventResponse.Types.TopicEventResponseStatus
                            .Success,
                        TopicResponseAction.Retry => C.TopicEventResponse.Types.TopicEventResponseStatus.Retry,
                        TopicResponseAction.Drop => C.TopicEventResponse.Types.TopicEventResponseStatus.Drop,
                        _ => throw new InvalidOperationException(
                            $"Unrecognized topic acknowledgement action: {action}")
                    }
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieves the subscription stream data from the Dapr sidecar.
    /// </summary>
    /// <param name="stream">The stream connection to and from the Dream sidecar instance.</param>
    /// <param name="channelWriter">The channel writer instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task FetchDataFromSidecarAsync(AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest> stream, ChannelWriter<TopicMessage> channelWriter, CancellationToken cancellationToken)
    {
        try
        {
            var initialRequest = new P.SubscribeTopicEventsInitialRequestAlpha1()
            {
                PubsubName = pubSubName,
                DeadLetterTopic = options?.DeadLetterTopic ?? string.Empty,
                Topic = topicName
            };

            if (options?.Metadata.Count > 0)
            {
                foreach (var (key, value) in options.Metadata)
                {
                    initialRequest.Metadata.Add(key, value);
                }
            }

            await stream.RequestStream.WriteAsync(new P.SubscribeTopicEventsRequestAlpha1 { InitialRequest = initialRequest }, cancellationToken);

            await foreach (var response in stream.ResponseStream.ReadAllAsync(cancellationToken))
            {
                var message = new TopicMessage(response.Id, response.Source, response.Type, response.SpecVersion, response.DataContentType, response.Topic, response.PubsubName)
                {
                    Path = response.Path,
                    Extensions = response.Extensions.Fields.ToDictionary(f => f.Key, kvp => kvp.Value)
                };
                
                await channelWriter.WriteAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //Ignore our own cancellation
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled &&
                                      cancellationToken.IsCancellationRequested)
        {
            //Ignore a remote cancellation due to our own cancellation
        }
        finally
        {
            channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Disposes the various resources associated with the instance.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await connectionManager.DisposeAsync();
        channel.Writer.Complete();
    }
}
