namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// The base implementation of a Dapr pub/sub client.
/// </summary>
public abstract class DaprPublishSubscribeClient
{
    /// <summary>
    /// Dynamically subscribes to a Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="messageHandler">The delegate reflecting the action to take upon messages received by the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public abstract Task<IAsyncDisposable> SubscribeAsync(string pubSubName, string topicName, DaprSubscriptionOptions options, TopicMessageHandler messageHandler, CancellationToken cancellationToken);
}
