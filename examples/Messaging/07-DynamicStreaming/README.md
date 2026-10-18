# Example 07: Imperative Dynamic Streaming Subscriptions (`Dapr.Messaging`)

This example demonstrates **imperative / dynamic runtime streaming subscriptions** using `DaprPublishSubscribeClient.SubscribeAsync`.

## Declarative vs. Imperative Subscriptions

In `Dapr.Messaging`, there are two primary ways to consume streaming subscriptions:

1. **Declarative (Recommended for fixed topics)**: Annotate `ITopicHandler<T>` with `[DaprTopic]`. `AddDaprMessaging()` automatically registers the background `StreamingSubscriberHostedService` which manages the subscription lifecycle and reconnects automatically.
2. **Imperative / Dynamic (This Example)**: Call `DaprPublishSubscribeClient.SubscribeAsync` directly with a delegate handler. Use this when:
   - Topics to subscribe to are determined dynamically at runtime (such as per-tenant topics or ephemeral channels).
   - You need custom supervision, backoff, or per-subscription `ErrorHandler` callbacks.
   - You need fine-grained control over when a subscription is opened and disposed.

## Using `SubscribeAsync`

```csharp
var options = new DaprSubscriptionOptions(
    new MessageHandlingPolicy(
        TimeoutDuration: TimeSpan.FromSeconds(10),
        DefaultResponseAction: TopicResponseAction.Retry))
{
    DeadLetterTopic = "tenant-events-dlq",
    ErrorHandler = ex =>
    {
        Console.WriteLine($"Subscription background fault: {ex.Message}");
        return Task.CompletedTask;
    }
};

await using var subscription = await messagingClient.SubscribeAsync(
    "pubsub",
    "tenant-events",
    options,
    HandleMessageAsync,
    cancellationToken);

// Await completion to observe stream lifecycle
await ((IDaprSubscription)subscription).Completion.WaitAsync(cancellationToken);
```

## Reconnection & Lifetime Management

`SubscribeAsync` returns an `IAsyncDisposable` that also implements `IDaprSubscription`. The `Completion` property exposes a `Task` representing the background stream lifecycle. When the connection closes or encounters a fatal error, `Completion` finishes, allowing your supervision loop to re-subscribe with backoff delay.

## Running the Example

```bash
dapr run --app-id dynamic-streaming-example \
         --dapr-grpc-port 50001 \
         -- dotnet run --project examples/Messaging/07-DynamicStreaming/DynamicStreaming.Example07.csproj
```

Send test requests using `DynamicStreaming.Example07.http`:

```bash
curl -X POST http://localhost:5000/publish/tenant-event \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"TENANT-1","eventType":"UserProvisioned","payload":"{}","timestamp":"2026-04-01T12:00:00Z"}'
```

## Testing

```bash
dotnet test examples/Messaging/07-DynamicStreaming/DynamicStreaming.Example07.Tests/DynamicStreaming.Example07.Tests.csproj
```
