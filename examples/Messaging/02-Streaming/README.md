# Example 02: Declarative Streaming Subscriptions (`Dapr.Messaging`)

This example demonstrates **declarative streaming pull pub/sub subscriptions** in `Dapr.Messaging` using the `[DaprTopic]` attribute and the `ITopicHandler<TMessage>` interface.

## Overview

In `Dapr.Messaging`, `DeliveryMode.Streaming` is the default and recommended delivery mode. It uses a bidirectional streaming gRPC connection (`SubscribeTopicEventsAlpha1`) directly to the Dapr sidecar.

### Key Advantages of Streaming Subscriptions

- **Zero Inbound Ports**: Your application does not open any HTTP or gRPC listening ports to receive pub/sub events.
- **Firewall & Ingress Friendly**: Ideal for worker services, background daemons, and secure private environments behind NAT/firewalls.
- **Reflection-Free & Native AOT**: Handlers and dispatchers are generated at compile time by the Roslyn incremental source generator (`Dapr.Messaging.Generators`).
- **Granular Acknowledgements**: Return `TopicResponseAction.Success`, `TopicResponseAction.Retry`, or `TopicResponseAction.Drop` to control sidecar behavior without throwing exceptions.
- **Automatic Reconnection**: The underlying `StreamingSubscriberHostedService` manages stream lifetimes and automatically reconnects if the connection drops.

## Writing a Topic Handler

Implement `ITopicHandler<TMessage>` and decorate the class with `[DaprTopic]`:

```csharp
[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
public class OrderProcessingHandler : ITopicHandler<OrderPlaced>
{
    private readonly ILogger<OrderProcessingHandler> logger;

    public OrderProcessingHandler(ILogger<OrderProcessingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(OrderPlaced order, TopicContext context, CancellationToken cancellationToken)
    {
        if (order.Quantity <= 0)
        {
            // Drop unprocessable / invalid messages to avoid poison message loops
            return Task.FromResult(TopicResponseAction.Drop);
        }

        if (order.ItemSku.StartsWith("RETRY-"))
        {
            // Request Dapr to redeliver following component retry policies
            return Task.FromResult(TopicResponseAction.Retry);
        }

        // Successfully acknowledge message processing
        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

## Acknowledgement Actions (`TopicResponseAction`)

| Action | Meaning | When to Use |
|---|---|---|
| `TopicResponseAction.Success` | Acknowledge (ACK) | Message processed successfully. Removed from broker queue. |
| `TopicResponseAction.Retry` | Negative ACK (NACK) | Transient error (database deadlock, temporary service outage). Dapr will redeliver. |
| `TopicResponseAction.Drop` | Reject / Discard | Fatal/poison message (unparseable or invalid schema). Dropped or routed to DLQ. |

## Registration in `Program.cs`

A single call to `AddDaprMessaging()` registers everything:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Discovers [DaprTopic] handlers at compile time, registers handlers in DI,
// and starts the StreamingSubscriberHostedService background service.
builder.Services.AddDaprMessaging();

var app = builder.Build();
app.Run();
```

## Running the Example

Run the application with the Dapr CLI:

```bash
dapr run --app-id streaming-example --dapr-grpc-port 50001 -- dotnet run --project examples/Messaging/02-Streaming/Streaming.Example02.csproj
```

Send test requests using `Streaming.Example02.http` or `curl`:

```bash
curl -X POST http://localhost:5000/publish/order \
  -H "Content-Type: application/json" \
  -d '{"orderId":"ORD-1001","customerId":"CUST-42","amount":149.99,"itemSku":"WIDGET-PRO-1","quantity":2}'
```

## Testing

Unit tests run entirely in-memory without Dapr sidecars or external brokers:

```bash
dotnet test examples/Messaging/02-Streaming/Streaming.Example02.Tests/Streaming.Example02.Tests.csproj
```
