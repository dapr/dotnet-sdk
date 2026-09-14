# Dapr.Messaging Examples

These are the worked examples for the Dapr.Messaging tutorial series demonstrating modern, AOT-capable, source-generator-driven Publish and Subscribe in .NET with Dapr.

- [01-Publishing](01-Publishing/) - Publishing typed events, custom CloudEvents, raw bytes, and bulk publishing with `IDaprPublishSubscribeClient`.
- [02-Streaming](02-Streaming/) - Declarative compile-time streaming subscriptions with `[DaprTopic]`, `ITopicHandler<T>`, and explicit acknowledgement actions (`Success`, `Retry`, `Drop`) with zero inbound ports required.
- [03-RoutingAndDeadLetter](03-RoutingAndDeadLetter/) - Content-based routing with CEL match expressions (`Match`, `Priority`) and dead-letter queue (DLQ) routing.
- [04-BulkSubscribe](04-BulkSubscribe/) - High-throughput batch message consumption with `BulkSubscribe = true`.
- [05-AppCallbackPush](05-AppCallbackPush/) - gRPC AppCallback push subscriptions (`DeliveryMode.Programmatic`) with `app.MapDaprAppCallback()`.
- [06-HttpSubscription](06-HttpSubscription/) - Declarative HTTP push subscriptions (`DeliveryMode.Http`) with custom application routes.
- [07-DynamicStreaming](07-DynamicStreaming/) - Imperative/dynamic streaming subscriptions with `DaprPublishSubscribeClient.SubscribeAsync` and custom `MessageHandlingPolicy` supervision loops.

## Key Differences from Legacy Pub/Sub (`Dapr.Client` / `Dapr.AspNetCore`)

| Feature | Legacy (`DaprClient` / `Dapr.AspNetCore`) | Modern (`Dapr.Messaging`) |
|---|---|---|
| **Discovery** | Runtime reflection over `EndpointDataSource` | Compile-time source generation (`[DaprTopic]`) with **zero runtime reflection** |
| **Native AOT** | Incompatible due to runtime reflection and unmapped JSON serializers | **100% Native AOT compatible** with source-generated dispatchers and JSON contexts |
| **Delivery Models** | HTTP push only (`MapSubscribeHandler` + `UseCloudEvents`) | **gRPC Streaming Pull** (default, no inbound ports), **gRPC AppCallback Push**, and **HTTP Push** |
| **Client Publishing** | Untyped `new DaprClientBuilder().Build()` | DI-first **`IDaprPublishSubscribeClient`** with `AddDaprMessaging()` |
| **Handler Model** | MVC / Minimal API controller endpoints | Interface-based **`ITopicHandler<TMessage>`** with constructor dependency injection |
| **Testability** | Required running ASP.NET Core server, sidecar, or WebApplicationFactory | Pure, isolated unit tests calling `handler.HandleAsync()` directly without any broker or sidecar |

## Prerequisites

- .NET 10 SDK (or .NET 8/9 SDK)
- Dapr runtime v1.18+ (for streaming subscription gRPC APIs)
- Dapr CLI (`dapr init`)

Each unit test in these examples runs with **no sidecar, no state store, no broker, and no Docker**.
