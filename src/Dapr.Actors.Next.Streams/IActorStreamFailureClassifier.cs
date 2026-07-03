namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Classifies forward-invoke failures into retryable and poison outcomes.
/// </summary>
public interface IActorStreamFailureClassifier
{
    /// <summary>
    /// Classifies an exception thrown while forwarding a subscription event.
    /// </summary>
    ActorStreamDeliveryAction Classify(Exception exception);
}
