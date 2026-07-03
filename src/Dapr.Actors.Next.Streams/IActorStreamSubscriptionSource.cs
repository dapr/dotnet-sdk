namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Opens Dapr pub/sub streaming subscriptions for actor stream delivery.
/// </summary>
public interface IActorStreamSubscriptionSource
{
    /// <summary>
    /// Subscribes to a topic stream and yields events that the component has assigned to this app instance.
    /// </summary>
    IAsyncEnumerable<ActorStreamEvent> SubscribeAsync(ActorStreamSubscription subscription, CancellationToken cancellationToken = default);
}
