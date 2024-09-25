using System.Text;
using Dapr.Messaging.PublishSubscribe;

var daprMessagingClientBuilder = new DaprPublishSubscribeClientBuilder();
var daprMessagingClient = daprMessagingClientBuilder.Build();

//Processor for each of the messages returned from the subscription
async Task<TopicResponseAction> HandleMessage(TopicMessage message, CancellationToken cancellationToken = default)
{
    try
    {
        //Do something with the message
        Console.WriteLine(Encoding.UTF8.GetString(message.Data.Span));
        return await Task.FromResult(TopicResponseAction.Success);
    }
    catch
    {
        return await Task.FromResult(TopicResponseAction.Retry);
    }
}

//Create a dynamic streaming subscription and subscribe with a timeout of 20 seconds
var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var subscription = await daprMessagingClient.SubscribeAsync("pubsub", "myTopic",
    new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry)),
    HandleMessage, cancellationTokenSource.Token);

await Task.Delay(TimeSpan.FromMinutes(1));

//When you're done with the subscription, simply dispose of it
await subscription.DisposeAsync();
