namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Describes the disposition returned to the pub/sub streaming subscription.
/// </summary>
public enum ActorStreamDeliveryAction
{
    /// <summary>
    /// Acknowledges the event after the actor turn completed successfully.
    /// </summary>
    Ack,

    /// <summary>
    /// Requests redelivery because the forward invoke failed transiently.
    /// </summary>
    Retry,

    /// <summary>
    /// Drops the event, allowing the component to dead-letter it when configured.
    /// </summary>
    Drop,
}
