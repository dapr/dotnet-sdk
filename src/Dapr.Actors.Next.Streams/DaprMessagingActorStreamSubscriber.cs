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
