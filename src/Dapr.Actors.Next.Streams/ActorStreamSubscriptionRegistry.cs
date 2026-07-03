namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Mutable actor stream subscription registry used by generated registration.
/// </summary>
public sealed class ActorStreamSubscriptionRegistry : IActorStreamSubscriptionRegistry
{
    private readonly List<ActorStreamSubscription> subscriptions = [];

    /// <inheritdoc />
    public IReadOnlyList<ActorStreamSubscription> Subscriptions => subscriptions;

    /// <summary>
    /// Adds a host-owned stream subscription.
    /// </summary>
    public ActorStreamSubscriptionRegistry Add(ActorStreamSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();
        subscriptions.Add(subscription);
        return this;
    }
}
