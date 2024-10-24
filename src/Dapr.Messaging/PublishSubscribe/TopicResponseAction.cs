namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Describes the various actions that can be taken on a topic message.
/// </summary>
public enum TopicResponseAction
{
    /// <summary>
    /// Indicates the message was processed successfully and should be deleted from the pub/sub topic.
    /// </summary>
    Success,
    /// <summary>
    /// Indicates a failure while processing the message and that the message should be resent for a retry.
    /// </summary>
    Retry,
    /// <summary>
    /// Indicates a failure while processing the message and that the message should be dropped or sent to the
    /// dead-letter topic if specified.
    /// </summary>
    Drop
}
