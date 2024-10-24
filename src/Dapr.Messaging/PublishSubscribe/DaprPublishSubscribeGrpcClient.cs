using P = Dapr.Client.Autogen.Grpc.v1.Dapr;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprPublishSubscribeGrpcClient : DaprPublishSubscribeClient
{
    private readonly P.DaprClient daprClient;

    /// <summary>
    /// Creates a new instance of a <see cref="DaprPublishSubscribeGrpcClient"/>
    /// </summary>
    public DaprPublishSubscribeGrpcClient(P.DaprClient client)
    {
        daprClient = client;
    }

    /// <summary>
    /// Dynamically subscribes to a Publish/Subscribe component and topic.
    /// </summary>
    /// <param name="pubSubName">The name of the Publish/Subscribe component.</param>
    /// <param name="topicName">The name of the topic to subscribe to.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="messageHandler">The delegate reflecting the action to take upon messages received by the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public override async Task<IAsyncDisposable> SubscribeAsync(string pubSubName, string topicName, DaprSubscriptionOptions options, TopicMessageHandler messageHandler, CancellationToken cancellationToken)
    {
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, daprClient);
        await receiver.SubscribeAsync(cancellationToken);
        return receiver;
    }
}

