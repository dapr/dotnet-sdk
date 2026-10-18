# Part 1: Publishing with IDaprPublishSubscribeClient

This example demonstrates how to publish messages using `IDaprPublishSubscribeClient` in the `Dapr.Messaging` package.

## Highlights

- **Dependency Injection**: Registered via `builder.Services.AddDaprMessaging()` and resolved via constructor or endpoint parameter.
- **Typed Publishing**: `PublishEventAsync<T>` serializes message payloads to JSON with standard CloudEvents wrapping.
- **Custom Options**: `PublishOptions` allows specifying custom CloudEvent attributes (e.g. `cloudevent.type`, `cloudevent.source`), message TTL (`ttlInSeconds`), and custom component metadata.
- **Bulk Publishing**: `BulkPublishEventAsync<T>` sends a batch of events in a single network round-trip.
- **Raw Byte Payloads**: `PublishByteEventAsync` sends binary payloads with custom content types.

## Running Locally

Start Dapr and run the application:

```powershell
cd examples\Messaging\01-Publishing
dapr run --app-id publishing-example-01 --dapr-grpc-port 50001 --dapr-http-port 3500 --app-port 5000 -- dotnet run
```

Then open `Publishing.Example01.http` in Rider or Visual Studio and run the sample requests.

## Running Tests

Run the unit tests:

```powershell
dotnet test Publishing.Example01.Tests\Publishing.Example01.Tests.csproj --no-restore
```

The tests verify event generation and publishing behavior using `Moq` against `IDaprPublishSubscribeClient` without requiring a running Dapr sidecar or broker.
