

using System.Text;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprPubSubClient();
var app = builder.Build();

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

var messagingClient = app.Services.GetRequiredService<DaprPublishSubscribeClient>();

//Create a dynamic streaming subscription and subscribe with a timeout of 30 seconds and 10 seconds for message handling
var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var subscription = await messagingClient.SubscribeAsync("pubsub", "myTopic",
    new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry)),
    HandleMessageAsync, cancellationTokenSource.Token);

await Task.Delay(TimeSpan.FromMinutes(1));

//When you're done with the subscription, simply dispose of it
await subscription.DisposeAsync();
