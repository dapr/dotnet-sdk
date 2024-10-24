using System.Text;
using Dapr.Messaging.PublishSubscribe;

var daprMessagingClientBuilder = new DaprPublishSubscribeClientBuilder(null);
var daprMessagingClient = daprMessagingClientBuilder.Build();

//Process each message returned from the subscription
Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken cancellationToken = default)
{
    try
    {
        //Do something with the message
        Console.WriteLine(Encoding.UTF8.GetString(message.Data.Span));
        return Task.FromResult(TopicResponseAction.Success);
    }
    catch
    {
        return Task.FromResult(TopicResponseAction.Retry);
    }
}

//Create a dynamic streaming subscription and subscribe with a timeout of 20 seconds and 10 seconds for message handling
var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var subscription = await daprMessagingClient.SubscribeAsync("pubsub", "myTopic",
    new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry)),
    HandleMessageAsync, cancellationTokenSource.Token);

await Task.Delay(TimeSpan.FromMinutes(1));

//When you're done with the subscription, simply dispose of it
await subscription.DisposeAsync();
