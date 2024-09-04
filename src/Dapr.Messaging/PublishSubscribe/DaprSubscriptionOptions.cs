namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Options used to configure the dynamic Dapr subscription.
/// </summary>
/// <param name="MessageHandlingPolicy">Describes the policy to take on messages that have not been acknowledged within the timeout period.</param>
public sealed record DaprSubscriptionOptions(MessageHandlingPolicy MessageHandlingPolicy)
{
    /// <summary>
    /// Subscription metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    
    /// <summary>
    /// The optional name of the dead-letter topic to send messages to.
    /// </summary>
    public string? DeadLetterTopic { get; init; }
}
