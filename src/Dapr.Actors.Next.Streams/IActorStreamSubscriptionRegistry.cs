namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Stores host-owned actor stream subscriptions discovered by generated registration.
/// </summary>
public interface IActorStreamSubscriptionRegistry
{
    /// <summary>
    /// Gets the actor stream subscriptions to open when the host starts.
    /// </summary>
    IReadOnlyList<ActorStreamSubscription> Subscriptions { get; }
}
