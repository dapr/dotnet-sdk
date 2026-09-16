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
- Docker / Container runtime (for running the Testcontainers integration tests)

## Testing Strategy for Dapr.Messaging

The examples illustrate a recommended two-tier testing approach for production services built with `Dapr.Messaging`:

### 1. Isolated Unit Tests (Fast, In-Memory)
Because `ITopicHandler<TMessage>` handlers are pure C# classes with explicit dependency injection, you can test all message handling logic, validations, and response actions (`Success`, `Retry`, `Drop`) using standard unit testing libraries like `Moq` or `NSubstitute`.
- **Zero infrastructure:** No Docker, no sidecar process, and no message broker needed.
- **Fast execution:** Millisecond-level test runs.

### 2. End-to-End Integration Tests (`Dapr.Testcontainers`)
Each example test project includes an integration test class demonstrating how to use `Dapr.Testcontainers` (`PubSubHarness` and `DaprTestApplicationBuilder`) to validate your messaging services against real containerized Dapr sidecars and Redis message brokers:
- **Delivery Mode Validation:** Verifies gRPC streaming pull (`DeliveryMode.Streaming`), gRPC push (`DeliveryMode.Programmatic`), and HTTP push (`DeliveryMode.Http`).
- **CEL Routing & Dead-Letter Queues:** Verifies that CloudEvent attribute evaluation, topic matching rules, and DLQ redirects operate identically to production.
- **Bulk Subscriptions & Custom Metadata:** Validates batching windows and delivery semantics against real Dapr components.

To run all unit and integration tests across the examples:
```bash
dotnet test examples/Messaging/01-Publishing/Publishing.Example01.Tests/Publishing.Example01.Tests.csproj
dotnet test examples/Messaging/02-Streaming/Streaming.Example02.Tests/Streaming.Example02.Tests.csproj
dotnet test examples/Messaging/03-RoutingAndDeadLetter/Routing.Example03.Tests/Routing.Example03.Tests.csproj
dotnet test examples/Messaging/04-BulkSubscribe/BulkSubscribe.Example04.Tests/BulkSubscribe.Example04.Tests.csproj
dotnet test examples/Messaging/05-AppCallbackPush/AppCallback.Example05.Tests/AppCallback.Example05.Tests.csproj
dotnet test examples/Messaging/06-HttpSubscription/HttpSubscription.Example06.Tests/HttpSubscription.Example06.Tests.csproj
dotnet test examples/Messaging/07-DynamicStreaming/DynamicStreaming.Example07.Tests/DynamicStreaming.Example07.Tests.csproj
```

