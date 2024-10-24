namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// The handler delegate responsible for processing the topic message.
/// </summary>
/// <param name="request">The message request to process.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The acknowledgement behavior to report back to the pub/sub endpoint about the message.</returns>
public delegate Task<TopicResponseAction> TopicMessageHandler(TopicMessage request,
    CancellationToken cancellationToken = default);
