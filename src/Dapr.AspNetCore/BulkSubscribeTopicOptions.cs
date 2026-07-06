namespace Dapr.AspNetCore;

/// <summary>
/// This class defines configurations for the bulk subscribe endpoint.
/// </summary>
public class BulkSubscribeTopicOptions
{
    /// <summary>
    /// Maximum number of messages in a bulk message from the message bus.
    /// </summary>
    public int MaxMessagesCount { get; set; } = 100;

    /// <summary>
    /// Maximum duration to wait for maxBulkSubCount messages by the message bus
    /// before sending the messages to Dapr.
    /// </summary>
    public int MaxAwaitDurationMs { get; set; } = 1000;
        
    /// <summary>
    /// The name of the topic to be bulk subscribed.
    /// </summary>
    public string TopicName { get; set; }
}