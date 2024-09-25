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
using Dapr.AppCallback.Autogen.Grpc.v1;
using Grpc.Core;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A thread-safe implementation of a receiver for messages from a specified Dapr publish/subscribe component and
/// topic.
/// </summary>
public sealed class PublishSubscribeReceiver : IAsyncDisposable
{
    private readonly static UnboundedChannelOptions UnboundedChannelOptions = new UnboundedChannelOptions
    {
        SingleWriter = true, SingleReader = true
    };

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
    private readonly Channel<TopicMessage> topicMessagesChannel;
    /// <summary>
    /// Maintains the various acknowledgements for each message.
    /// </summary>
    private readonly Channel<TopicAcknowledgement> acknowledgementsChannel = Channel.CreateUnbounded<TopicAcknowledgement>(UnboundedChannelOptions);
    /// <summary>
    /// The stream connection between this instance and the Dapr sidecar.
    /// </summary>
    private AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>? clientStream;
    /// <summary>
    /// Used to ensure thread-safe operations against the stream.
    /// </summary>
    private readonly SemaphoreSlim semaphore = new(1, 1);
    /// <summary>
    /// The handler delegate responsible for processing the topic messages.
    /// </summary>
    private readonly TopicMessageHandler messageHandler;
    /// <summary>
    /// A reference to the DaprClient instance.
    /// </summary>
    private readonly P.Dapr.DaprClient client;
    /// <summary>
    /// Flag that prevents the developer from accidentally initializing the subscription more than once from the same receiver.
    /// </summary>
    private bool hasInitialized;
    /// <summary>
    /// Flag that ensures the instance is only disposed a single time.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Constructs a new instance of a <see cref="PublishSubscribeReceiver"/> instance.
    /// </summary>
    /// <param name="pubSubName">The name of the Dapr Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Options allowing the behavior of the receiver to be configured.</param>
    /// <param name="handler">The delegate reflecting the action to take upon messages received by the subscription.</param>
    /// <param name="client">A reference to the DaprClient instance.</param>
    internal PublishSubscribeReceiver(string pubSubName, string topicName, DaprSubscriptionOptions options, TopicMessageHandler handler, P.Dapr.DaprClient client)
    {
        this.client = client;
        this.pubSubName = pubSubName;
        this.topicName = topicName;
        this.options = options;
        this.messageHandler = handler;
        topicMessagesChannel = options.MaximumQueuedMessages is > 0
            ? Channel.CreateBounded<TopicMessage>(new BoundedChannelOptions((int)options.MaximumQueuedMessages)
            {
                SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
            })
            : Channel.CreateUnbounded<TopicMessage>(UnboundedChannelOptions);
    }

    /// <summary>
    /// Dynamically subscribes to messages on a PubSub topic provided by the Dapr sidecar.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TopicMessage}"/> containing messages provided by the sidecar.</returns>
    internal async Task SubscribeAsync(CancellationToken cancellationToken = default)
    {
        //Prevents the receiver from performing the subscribe operation more than once (as the multiple initialization messages would cancel the stream).
        if (hasInitialized)
            return;
        hasInitialized = true;

        var stream = await GetStreamAsync(cancellationToken);

        //Retrieve the messages from the sidecar and write to the messages channel
        var fetchMessagesTask = FetchDataFromSidecarAsync(stream, topicMessagesChannel.Writer, cancellationToken);

        //Process the messages as they're written to either channel
        var acknowledgementProcessorTask = ProcessAcknowledgementChannelMessagesAsync(cancellationToken);
        var topicMessageProcessorTask = ProcessTopicChannelMessagesAsync(cancellationToken);

        try
        {
            await Task.WhenAll(fetchMessagesTask, acknowledgementProcessorTask, topicMessageProcessorTask);
        }
        catch (OperationCanceledException)
        {
            // Will be cleaned up during DisposeAsync
        }
    }

    /// <summary>
    /// Retrieves or creates the bidirectional stream to the DaprClient for transacting pub/sub subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stream connection.</returns>
    private async Task<AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>> GetStreamAsync(CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            return clientStream ??= client.SubscribeTopicEventsAlpha1(cancellationToken: cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Acknowledges the indicated message back to the Dapr sidecar with an indicated behavior to take on the message.
    /// </summary>
    /// <param name="messageId">The identifier of the message the behavior is in reference to.</param>
    /// <param name="behavior">The behavior to take on the message as indicated by either the message handler or timeout message handling configuration.</param>
    /// <returns></returns>
    private async Task AcknowledgeMessageAsync(string messageId, TopicResponseAction behavior)
    {
        var action = behavior switch
        {
            TopicResponseAction.Success => TopicEventResponse.Types.TopicEventResponseStatus.Success,
            TopicResponseAction.Retry => TopicEventResponse.Types.TopicEventResponseStatus.Retry,
            TopicResponseAction.Drop => TopicEventResponse.Types.TopicEventResponseStatus.Drop,
            _ => throw new InvalidOperationException(
                $"Unrecognized topic acknowledgement action: {behavior}")
        };

        var acknowledgement = new TopicAcknowledgement(messageId, action);
        await acknowledgementsChannel.Writer.WriteAsync(acknowledgement);
    }

    /// <summary>
    /// Processes each acknowledgement from the acknowledgement channel reader as it's populated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ProcessAcknowledgementChannelMessagesAsync(CancellationToken cancellationToken)
    {
        var messageStream = await GetStreamAsync(cancellationToken);
        await foreach (var acknowledgement in acknowledgementsChannel.Reader.ReadAllAsync(cancellationToken))
        {    
            await messageStream.RequestStream.WriteAsync(new P.SubscribeTopicEventsRequestAlpha1
            {
                EventProcessed = new()
                {
                    Id = acknowledgement.MessageId,
                    Status = new() { Status = acknowledgement.Action }
                }
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Processes each topic messages from the channel as it's populated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ProcessTopicChannelMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in topicMessagesChannel.Reader.ReadAllAsync(cancellationToken))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(options.MessageHandlingPolicy.TimeoutDuration);

            //Evaluate the message and return an acknowledgement result
            var messageAction = await messageHandler(message, cts.Token);

            try
            {
                //Share the result with the sidecar
                await AcknowledgeMessageAsync(message.Id, messageAction);
            }
            catch (OperationCanceledException)
            {
                //Acknowledge the message using the configured default response action
                await AcknowledgeMessageAsync(message.Id, options.MessageHandlingPolicy.DefaultResponseAction);
            }
        }
    }

    /// <summary>
    /// Retrieves the subscription stream data from the Dapr sidecar.
    /// </summary>
    /// <param name="stream">The stream connection to and from the Dapr sidecar instance.</param>
    /// <param name="channelWriter">The channel writer instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task FetchDataFromSidecarAsync(
        AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1> stream,
        ChannelWriter<TopicMessage> channelWriter, CancellationToken cancellationToken)
    {
        //Build out the initial topic events request
        var initialRequest = new P.SubscribeTopicEventsRequestInitialAlpha1()
        {
            PubsubName = pubSubName, DeadLetterTopic = options.DeadLetterTopic ?? string.Empty, Topic = topicName
        };

        if (options.Metadata.Count > 0)
        {
            foreach (var (key, value) in options.Metadata)
            {
                initialRequest.Metadata.Add(key, value);
            }
        }
        
        //Send this request to the Dapr sidecar
        await stream.RequestStream.WriteAsync(
            new P.SubscribeTopicEventsRequestAlpha1 { InitialRequest = initialRequest }, cancellationToken);

        //Each time a message is received from the stream, push it into the topic messages channel
        await foreach (var response in stream.ResponseStream.ReadAllAsync(cancellationToken))
        {
            var message =
                new TopicMessage(response.EventMessage.Id, response.EventMessage.Source, response.EventMessage.Type,
                    response.EventMessage.SpecVersion, response.EventMessage.DataContentType,
                    response.EventMessage.Topic, response.EventMessage.PubsubName)
                {
                    Path = response.EventMessage.Path,
                    Extensions = response.EventMessage.Extensions.Fields.ToDictionary(f => f.Key, kvp => kvp.Value)
                };

            try
            {
                await channelWriter.WaitToWriteAsync(cancellationToken);
                await channelWriter.WriteAsync(message, cancellationToken);
            }
            catch (Exception)
            {
                // Handle being unable to write because the writer is completed due to an active DisposeAsync operation
            }
        }
    }

    /// <summary>
    /// Disposes the various resources associated with the instance.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
            return;
        isDisposed = true;

        //Stop processing new events - we'll leave any messages yet unseen as unprocessed and
        //Dapr will handle as necessary when they're not acknowledged
        topicMessagesChannel.Writer.Complete();
        
        //Flush the remaining acknowledgements, but start by marking the writer as complete so it doesn't receive any other messages either
        acknowledgementsChannel.Writer.Complete();
        
        try
        {
            //Process any remaining acknowledgements on the channel without exceeding the configured maximum clean up time
            await acknowledgementsChannel.Reader.Completion.WaitAsync(options.MaximumCleanupTimeout);
        }
        catch (OperationCanceledException)
        {
            //Handled
        }
    }

    /// <summary>
    /// Reflects the action to take on a given message identifier.
    /// </summary>
    /// <param name="MessageId">The identifier of the message.</param>
    /// <param name="Action">The action to take on the message in the acknowledgement request.</param>
    private sealed record TopicAcknowledgement(string MessageId, TopicEventResponse.Types.TopicEventResponseStatus Action);
}
