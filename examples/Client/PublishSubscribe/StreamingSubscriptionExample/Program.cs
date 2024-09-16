using Dapr.Messaging.PublishSubscribe;

var daprMessagingClientBuilder = new DaprPublishSubscribeClientBuilder();
var daprMessagingClient = daprMessagingClientBuilder.Build();

//Processor for each of the messages returned from the subscription
async Task<TopicResponseAction> HandleMessage(TopicMessage message, CancellationToken cancellationToken = default)
{
    try
    {
        //Do something with the message
        return await Task.FromResult(TopicResponseAction.Success);
    }
    catch
    {
        return await Task.FromResult(TopicResponseAction.Retry);
    }
}

//Create a dynamic streaming subscription
var subscription = daprMessagingClient.Register("pubsub", "myTopic",
    new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(15), TopicResponseAction.Retry)),
    HandleMessage, CancellationToken.None);

//Subscribe to messages on it with a timeout of 30 seconds
var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await subscription.SubscribeAsync(cancellationTokenSource.Token);

await Task.Delay(TimeSpan.FromMinutes(1));

//When you're done with the subscription, simply dispose of it
await subscription.DisposeAsync();
