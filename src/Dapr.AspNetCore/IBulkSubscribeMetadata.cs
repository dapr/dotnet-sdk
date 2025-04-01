namespace Dapr.AspNetCore;

/// <summary>
/// Bulk Subscribe Metadata that describes bulk subscribe configuration options.
/// </summary>
public interface IBulkSubscribeMetadata
{
    /// <summary>
    /// Gets the maximum number of messages in a bulk message from the message bus.
    /// </summary>
    int MaxMessagesCount { get; }
        
    /// <summary>
    /// Gets the Maximum duration to wait for maxBulkSubCount messages by the message bus
    /// before sending the messages to Dapr.
    /// </summary>
    int MaxAwaitDurationMs { get; }
        
    /// <summary>
    /// The name of the topic to be bulk subscribed.
    /// </summary>
    public string TopicName { get; }
}