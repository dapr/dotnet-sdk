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

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dapr.Client.Autogen.Grpc.v1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// Maintains the stream connection to the Dapr sidecar for the subscription.
    /// </summary>
    private readonly ConnectionManager connectionManager;
    /// <summary>
    /// Used for logging purposes.
    /// </summary>
    private readonly ILogger<PublishSubscribeReceiver>? logger;

    /// <summary>
    /// The name of the Dapr pubsub component.
    /// </summary>
    private readonly string pubsubName;
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
    /// A collection of <see cref="TaskCompletionSource"/> used to signal acknowledgement of received messages so a status
    /// can be sent back to the sidecar indicating what behavior should happen to each.
    /// </summary>
    private readonly Dictionary<string, TaskCompletionSource<bool>> acknowledgementTasks = new();
    /// <summary>
    /// A semaphore used to ensure thread-safe access to the <see cref="acknowledgementTasks"/> dictionary.
    /// </summary>
    private readonly SemaphoreSlim acknowledgementSemaphore = new(1, 1);
    
    /// <summary>
    /// Constructs a new instance of a <see cref="PublishSubscribeReceiver"/> instance.
    /// </summary>
    /// <param name="pubsubName">The name of the Dapr pubsub component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Options allowing the behavior of the receiver to be configured.</param>
    /// <param name="daprClient"></param>
    /// <param name="loggerFactory">Used to create the logger instance.</param>
    internal PublishSubscribeReceiver(string pubsubName, string topicName, DaprSubscriptionOptions options, P.Dapr.DaprClient daprClient, ILoggerFactory? loggerFactory)
    {
        this.pubsubName = pubsubName;
        this.topicName = topicName;
        this.options = options;
        connectionManager = new ConnectionManager(daprClient);

        logger = loggerFactory?.CreateLogger<PublishSubscribeReceiver>() ??
                  NullLoggerFactory.Instance.CreateLogger<PublishSubscribeReceiver>();
    }

    /// <summary>
    /// Dynamically subscribes to messages on a PubSub topic provided by the Dapr sidecar.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TopicMessage}"/> containing messages provided by the sidecar.</returns>
    public IAsyncEnumerable<TopicMessage> SubscribeAsync(CancellationToken cancellationToken)
    {
        _ = FetchDataFromSidecar(channel.Writer, cancellationToken);
        return ReadMessagesFromChannelAsync(channel.Reader, cancellationToken);
    }

    /// <summary>
    /// Specifies the action that should be taken on the message after processing it.
    /// </summary>
    /// <param name="messageId">The identifier of the message to acknowledge.</param>
    /// <param name="messageAction">The action to take on the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AcknowledgeMessageAsync(string messageId, TopicMessageAction messageAction, CancellationToken cancellationToken)
    {
        var stream = await connectionManager.GetStreamAsync(cancellationToken);
        await stream.RequestStream.WriteAsync(new SubscribeTopicEventsRequestAlpha1
        {
            EventResponse = new SubscribeTopicEventsResponseAlpha1
            {
                Id = messageId,
                Status = new C.TopicEventResponse
                {
                    Status = messageAction switch
                    {
                        TopicMessageAction.Retry => C.TopicEventResponse.Types.TopicEventResponseStatus.Retry,
                        TopicMessageAction.Success => C.TopicEventResponse.Types.TopicEventResponseStatus.Success,
                        TopicMessageAction.Drop => C.TopicEventResponse.Types.TopicEventResponseStatus.Drop,
                        _ => throw new ArgumentOutOfRangeException(nameof(messageAction), messageAction, null)
                    }
                }
            }
        }, cancellationToken);

        await acknowledgementSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (acknowledgementTasks.TryGetValue(messageId, out var tcs))
            {
                tcs.SetResult(true);
                acknowledgementTasks.Remove(messageId);
            }
        }
        finally
        {
            acknowledgementSemaphore.Release();
        }
    }

    /// <summary>
    /// Reads the topic messages returned from the Dapr sidecar.
    /// </summary>
    /// <param name="reader">The channel reader instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TopicMessage}"/> containing the received messages from the Dapr sidecar.</returns>
    private async IAsyncEnumerable<TopicMessage> ReadMessagesFromChannelAsync(ChannelReader<TopicMessage> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var message))
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(options!.MessageHandlingPolicy.TimeoutDuration);

                yield return message;

                try
                {
                    //Wait for the message to be acknowledged
                    await WaitForAcknowledgementAsync(message.Id, cts.Token);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    //Handle the acknowledgement timeout using the specified default policy
                    await AcknowledgeMessageAsync(message.Id, options.MessageHandlingPolicy.DefaultMessageAction,
                        cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Sets up a timeout for message acknowledgement before the configured default action is applied
    /// to each message.
    /// </summary>
    /// <param name="messageId">The identifier of the topic message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    private async Task WaitForAcknowledgementAsync(string messageId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await acknowledgementSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            acknowledgementTasks[messageId] = tcs;
        }
        finally
        {
            acknowledgementSemaphore.Release();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.MessageHandlingPolicy.TimeoutDuration);
        
        await using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            await tcs.Task;
        }
    }

    /// <summary>
    /// Retrieves the subscription stream data from the Dapr sidecar.
    /// </summary>
    /// <param name="channelWriter">The channel writer instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task FetchDataFromSidecar(ChannelWriter<TopicMessage> channelWriter, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await connectionManager.GetStreamAsync(cancellationToken);
            var initialRequest = new P.SubscribeTopicEventsInitialRequestAlpha1()
            {
                PubsubName = pubsubName,
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

            await stream.RequestStream.WriteAsync(new SubscribeTopicEventsRequestAlpha1 { InitialRequest = initialRequest }, cancellationToken);

            await foreach (var response in stream.ResponseStream.ReadAllAsync(cancellationToken))
            {
                var message = new TopicMessage
                {
                    Id = response.Id,
                    Source = response.Source,
                    Type = response.Type,
                    SpecVersion = response.SpecVersion,
                    DataContentType = response.DataContentType,
                    Data = response.Data.Memory,
                    Topic = response.Topic,
                    PubSubName = response.PubsubName,
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
        acknowledgementSemaphore.Dispose();
    }
}

/// <summary>
/// 
/// </summary>
public sealed class PublishSubscribeReceiverBuilder
{
    private readonly ILoggerFactory? loggerFactory;
    private readonly P.Dapr.DaprClient daprClient;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="daprClient"></param>
    public PublishSubscribeReceiverBuilder(ILoggerFactory? loggerFactory, P.Dapr.DaprClient daprClient)
    {
        this.loggerFactory = loggerFactory;
        this.daprClient = daprClient;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pubsubName"></param>
    /// <param name="topicName"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public PublishSubscribeReceiver Build(string pubsubName, string topicName,
        DaprSubscriptionOptions options) =>
        new(pubsubName, topicName, options, daprClient, loggerFactory);
}
