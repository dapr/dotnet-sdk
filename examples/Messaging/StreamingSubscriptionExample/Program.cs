
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

// The reconnection loop re-establishes the subscription after a stream termination (sidecar disconnect,
// unparseable message, etc.). HandleMessageAsync handles message-level retry via TopicResponseAction.Retry,
// but stream-level reconnection requires re-calling SubscribeAsync. The supervisor resets hasInitialized
// on any termination, so re-subscription is always possible.
while (!cancellationTokenSource.Token.IsCancellationRequested)
{
    var subscription = await messagingClient.SubscribeAsync("pubsub", "myTopic",
        new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry)),
        HandleMessageAsync, cancellationTokenSource.Token);

    // Log disconnects if the developer wants observability. The returned IAsyncDisposable also
    // implements IDaprSubscription; cast to access Completion. Completion faults only when the
    // ErrorHandler is absent (or itself throws); a handled fault completes normally.
    if (subscription is IDaprSubscription observable)
    {
        _ = observable.Completion.ContinueWith(t =>
        {
            if (t.IsFaulted)
                Console.WriteLine($"Subscription terminated: {t.Exception?.InnerException?.Message}");
        }, TaskScheduler.Default);
    }

    // Wait for the subscription to terminate (fault, clean stream end, or cancellation).
    if (subscription is IDaprSubscription awaitable)
    {
        try { await awaitable.Completion.WaitAsync(cancellationTokenSource.Token); }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Subscription faulted: {ex.Message}. Reconnecting...");
        }
    }

    await subscription.DisposeAsync();
}

