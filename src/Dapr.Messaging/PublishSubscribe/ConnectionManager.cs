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
/// Maintains the streaming connection to the Dapr sidecar so it can be repurposed without
/// multiple callers opening separate connections.
/// </summary>
internal sealed class ConnectionManager : IAsyncDisposable
{
    /// <summary>
    /// A reference to the DaprClient instance.
    /// </summary>
    private readonly P.Dapr.DaprClient client;
    /// <summary>
    /// Used to ensure thread-safe operations against the stream.
    /// </summary>
    private readonly SemaphoreSlim semaphore = new(1, 1);
    /// <summary>
    /// The stream connection between this instance and the Dapr sidecar.
    /// </summary>
    private AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest>? stream;
    /// <summary>
    /// Maintains the various acknowledgements for each message.
    /// </summary>
    /// <remarks>
    /// Storing the acknowledgements here so we can ensure that a single writer handles sending them back over the stream. This class
    /// isn't public 
    /// </remarks>
    private readonly Channel<TopicAcknowledgement> acknowledgements = Channel.CreateUnbounded<TopicAcknowledgement>();

    public ConnectionManager(P.Dapr.DaprClient client)
    {
        this.client = client;

        //Processes each acknowledgement from the channel reader as they are provided by the various PublishSubscribeReceiver instances.
        Task.Run(async () =>
        {
            await foreach (var acknowledgement in acknowledgements.Reader.ReadAllAsync())
            {
                await ProcessAcknowledgementAsync(acknowledgement);
            }
        });
    }

    /// <summary>
    /// Retrieves or creates the bidirectional stream to the DaprClient for transacting pub/sub subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task<AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, C.TopicEventRequest>> GetStreamAsync(CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            return stream ??= client.SubscribeTopicEventsAlpha1(cancellationToken: cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Acknowledges a provided message with an intended action to perform on it based on how it was either
    /// handled or whether it timed out.
    /// </summary>
    /// <param name="messageId">The identifier of the message the behavior is in reference to.</param>
    /// <param name="behavior">The behavior to take on the message.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task AcknowledgeMessageAsync(string messageId, TopicResponseAction behavior)
    {
        var action = behavior switch
        {
            TopicResponseAction.Success => C.TopicEventResponse.Types.TopicEventResponseStatus
                .Success,
            TopicResponseAction.Retry => C.TopicEventResponse.Types.TopicEventResponseStatus.Retry,
            TopicResponseAction.Drop => C.TopicEventResponse.Types.TopicEventResponseStatus.Drop,
            _ => throw new InvalidOperationException(
                $"Unrecognized topic acknowledgement action: {behavior}")
        };

        var acknowledgement = new TopicAcknowledgement(messageId, action);
        await acknowledgements.Writer.WriteAsync(acknowledgement);
    }

    /// <summary>
    /// Processes each of the acknowledgement messages.
    /// </summary>
    /// <param name="acknowledgement">Information about the message and the action to take on it.</param>
    private async Task ProcessAcknowledgementAsync(TopicAcknowledgement acknowledgement)
    {
        var messageStream = await GetStreamAsync(CancellationToken.None);
        await messageStream.RequestStream.WriteAsync(new P.SubscribeTopicEventsRequestAlpha1
        {
            EventResponse = new()
            {
                Id = acknowledgement.MessageId, Status = new() { Status = acknowledgement.Action }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        //Flush the remaining acknowledgements, but start by marking the writer as complete so we don't get any more of them
        acknowledgements.Writer.Complete(); 
        await foreach (var message in acknowledgements.Reader.ReadAllAsync())
        {
            await ProcessAcknowledgementAsync(message);
        }
        
        if (stream is not null)
        {
            await stream.RequestStream.CompleteAsync();
        }

        semaphore.Dispose();
    }

    /// <summary>
    /// Reflects the action to take on a given message identifier.
    /// </summary>
    /// <param name="MessageId">The identifier of the message.</param>
    /// <param name="Action">The action to take on the message in the acknowledgement request.</param>
    private sealed record TopicAcknowledgement(string MessageId, C.TopicEventResponse.Types.TopicEventResponseStatus Action);
}
