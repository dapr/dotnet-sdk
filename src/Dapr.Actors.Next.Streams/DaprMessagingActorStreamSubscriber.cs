// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Messaging.PublishSubscribe;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Opens dynamic Dapr.Messaging subscriptions and forwards received topic messages to actors.
/// </summary>
public sealed class DaprMessagingActorStreamSubscriber(
    DaprPublishSubscribeClient client,
    ActorStreamSubscriptionRunner runner)
{
    /// <summary>
    /// Opens one Dapr.Messaging dynamic subscription for an actor stream subscription.
    /// </summary>
    public Task<IAsyncDisposable> SubscribeAsync(ActorStreamSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(subscription.MessageTimeout, TopicResponseAction.Retry))
        {
            Metadata = subscription.Metadata,
            DeadLetterTopic = subscription.DeadLetterTopic,
            MaximumQueuedMessages = subscription.MaximumQueuedMessages,
        };

        return client.SubscribeAsync(
            subscription.PubsubName,
            subscription.Topic,
            options,
            async (message, ct) =>
            {
                var evt = ActorStreamTopicMessageMapper.ToActorStreamEvent(message);
                var action = await runner.ProcessEventAsync(subscription, evt, ct).ConfigureAwait(false);
                return ToTopicResponseAction(action);
            },
            cancellationToken);
    }

    private static TopicResponseAction ToTopicResponseAction(ActorStreamDeliveryAction action) =>
        action switch
        {
            ActorStreamDeliveryAction.Ack => TopicResponseAction.Success,
            ActorStreamDeliveryAction.Retry => TopicResponseAction.Retry,
            ActorStreamDeliveryAction.Drop => TopicResponseAction.Drop,
            _ => TopicResponseAction.Retry,
        };
}
