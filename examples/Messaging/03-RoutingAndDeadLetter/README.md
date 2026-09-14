# Example 03: Content-Based Routing and Dead-Letter Topics (`Dapr.Messaging`)

This example demonstrates **content-based routing using Common Expression Language (CEL)**, **priority ordering**, and **Dead-Letter Queue (DLQ)** configuration in `Dapr.Messaging`.

## Overview

In event-driven architectures, different messages published to the same topic often require distinct handling logic based on payload content (such as priority tier or geographic region) or need fallback to a Dead-Letter Queue upon failure.

`Dapr.Messaging` provides declarative configuration for:
1. **CEL Matching Rules (`Match`)**: Evaluates CloudEvents attributes and message payload properties.
2. **Rule Priority (`Priority`)**: Integer priority establishing the evaluation order (lower number = higher precedence).
3. **Dead-Letter Topics (`DeadLetterTopic`)**: Topic to which messages rejected with `TopicResponseAction.Drop` or exceeding retry limits are forwarded.
4. **Topic Metadata (`[DaprTopicMetadata]`)**: Custom key-value pairs forwarded with the subscription manifest.

## Configuring Content-Based Routing

### 1. High-Priority Express Handler

```csharp
[DaprTopic("pubsub", "express-shipments", Match = "event.data.PriorityTier == 'express'", Priority = 1, DeadLetterTopic = "deadletter-shipments")]
[DaprTopicMetadata("routingType", "express-tier")]
public class ExpressShippingHandler : ITopicHandler<ShipmentPackage>
{
    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken ct)
    {
        // Process urgent express shipment
        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

### 2. International Destination Handler

```csharp
[DaprTopic("pubsub", "international-shipments", Match = "event.data.DestinationCountry != 'US'", Priority = 1, DeadLetterTopic = "deadletter-shipments")]
[DaprTopicMetadata("routingType", "international-customs")]
public class InternationalShippingHandler : ITopicHandler<ShipmentPackage>
{
    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken ct)
    {
        // Process international customs clearance
        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

### 3. Fallback Catch-All Handler

```csharp
[DaprTopic("pubsub", "standard-shipments", Priority = 10, DeadLetterTopic = "deadletter-shipments")]
[DaprTopicMetadata("routingType", "standard-domestic")]
public class StandardShippingHandler : ITopicHandler<ShipmentPackage>
{
    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken ct)
    {
        // Catch-all handler for domestic standard shipments
        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

### 4. Dead-Letter Queue (DLQ) Handler

```csharp
[DaprTopic("pubsub", "deadletter-shipments")]
public class DeadLetterShipmentHandler : ITopicHandler<ShipmentPackage>
{
    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken ct)
    {
        // Receives messages dropped by other handlers
        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

## How Dapr Evaluates Routing Rules

```
Incoming Event on "express-shipments"
           │
           ▼
  Match: PriorityTier == 'express'? (Priority = 1)
  ├── Yes ──► ExpressShippingHandler
  └── No / Dropped ──► DeadLetterShipmentHandler ("deadletter-shipments")
```

## Running the Example

```bash
dapr run --app-id routing-example --dapr-grpc-port 50001 -- dotnet run --project examples/Messaging/03-RoutingAndDeadLetter/Routing.Example03.csproj
```

Send test requests using `Routing.Example03.http`:
- Request 1 routes to `ExpressShippingHandler`.
- Request 2 routes to `InternationalShippingHandler`.
- Request 3 routes to `StandardShippingHandler`.
- Request 4 simulates invalid weight and drops the message to `DeadLetterShipmentHandler`.

## Testing

```bash
dotnet test examples/Messaging/03-RoutingAndDeadLetter/Routing.Example03.Tests/Routing.Example03.Tests.csproj
```
