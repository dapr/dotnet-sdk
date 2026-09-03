using System.Text;
using System.Text.Json;
using Dapr;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprPubSubClient();
var app = builder.Build();

// The message handler processes each topic message and returns the action the sidecar should take.
// Returning Retry asks Dapr to redeliver the message; Success acknowledges it; Drop discards it.
// Catch specific exceptions rather than swallowing everything — an unparseable message, for
// example, should be dropped (not retried) to avoid a poison-message loop.
Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken cancellationToken)
{
    try
    {
        var body = Encoding.UTF8.GetString(message.Data.Span);
        Console.WriteLine($"Received: {body}");
        return Task.FromResult(TopicResponseAction.Success);
    }
    catch (JsonException)
    {
        // Malformed payload — don't retry, drop it.
        return Task.FromResult(TopicResponseAction.Drop);
    }
    catch (Exception ex)
    {
        // Transient failure — let Dapr redeliver.
        Console.WriteLine($"Message handler error: {ex.Message}");
        return Task.FromResult(TopicResponseAction.Retry);
    }
}

var messagingClient = app.Services.GetRequiredService<DaprPublishSubscribeClient>();

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// Reconnection loop: re-establishes the subscription after any stream termination
// (sidecar restart, network blip, background-task fault, etc.). The supervisor
// resets its state on every termination, so re-calling SubscribeAsync always works.
while (!cts.Token.IsCancellationRequested)
{
    var options = new DaprSubscriptionOptions(
        new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry))
    {
        // The ErrorHandler receives background faults (e.g. the gRPC stream drops).
        // If configured, it is invoked once per fault and Completion completes
        // normally — the loop continues and re-subscribes. If the handler itself
        // throws, Completion faults with an AggregateException (caught below).
        ErrorHandler = ex =>
        {
            Console.WriteLine($"Subscription error: {ex.InnerException?.Message ?? ex.Message}");
            return Task.CompletedTask;
        }
    };

    await using var subscription = await messagingClient.SubscribeAsync(
        "pubsub", "myTopic", options, HandleMessageAsync, cts.Token);

    // The returned IAsyncDisposable also implements IDaprSubscription.
    // Await Completion to block until the subscription terminates.
    var completion = ((IDaprSubscription)subscription).Completion;

    try
    {
        await completion.WaitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Caller cancelled — exit the loop.
        break;
    }
    catch (AggregateException ex)
    {
        // The ErrorHandler itself threw — its exception is combined with the original DaprException.
        Console.WriteLine($"Subscription faulted (handler error): {ex.Flatten().InnerException?.Message}");
    }
    catch (DaprException ex)
    {
        // No ErrorHandler was configured, or the fault could not be handled.
        Console.WriteLine($"Subscription faulted: {ex.InnerException?.Message ?? ex.Message}");
    }

    // Brief delay before reconnecting to avoid a tight loop if the sidecar is down.
    try { await Task.Delay(TimeSpan.FromSeconds(3), cts.Token); }
    catch (OperationCanceledException) { break; }
}