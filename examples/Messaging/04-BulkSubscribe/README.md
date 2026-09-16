# Example 04: Bulk Subscriptions (`Dapr.Messaging`)

This example demonstrates **bulk pub/sub subscriptions** in `Dapr.Messaging`, configured via `BulkSubscribe = true` on the `[DaprTopic]` attribute.

## Overview

Bulk pub/sub subscriptions optimize high-throughput message ingestion (such as IoT telemetry, logs, or financial tick streams). Instead of delivering messages one-by-one, the Dapr sidecar batches incoming messages from the underlying broker and dispatches them efficiently.

### Bulk Subscription Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `BulkSubscribe` | `bool` | `false` | Enables bulk delivery for the topic subscription. |
| `MaxMessagesCount` | `int` | `100` | Maximum number of messages to accumulate before delivering a batch. |
| `MaxAwaitDurationMs` | `int` | `1000` | Maximum duration (in milliseconds) Dapr waits before flushing a partial batch. |

## Writing a Bulk Topic Handler

```csharp
[DaprTopic("pubsub", "telemetry", BulkSubscribe = true, MaxMessagesCount = 50, MaxAwaitDurationMs = 500)]
public class TelemetryBulkHandler : ITopicHandler<DeviceTelemetry>
{
    private readonly ILogger<TelemetryBulkHandler> logger;

    public TelemetryBulkHandler(ILogger<TelemetryBulkHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(DeviceTelemetry reading, TopicContext context, CancellationToken ct)
    {
        if (reading.TemperatureCelsius < -273.15 || reading.HumidityPercent < 0 || reading.HumidityPercent > 100)
        {
            // Drop invalid/corrupt sensor readings
            return Task.FromResult(TopicResponseAction.Drop);
        }

        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

## Running the Example

```bash
dapr run --app-id bulk-subscribe-example --dapr-grpc-port 50001 -- dotnet run --project examples/Messaging/04-BulkSubscribe/BulkSubscribe.Example04.csproj
```

Send single or batch publish requests using `BulkSubscribe.Example04.http`:

```bash
curl -X POST http://localhost:5000/publish/telemetry-batch \
  -H "Content-Type: application/json" \
  -d '[{"deviceId":"SENSOR-1","temperatureCelsius":22.4,"humidityPercent":48.0,"timestampUnixMs":1711800000000}]'
```

## Testing

```bash
dotnet test examples/Messaging/04-BulkSubscribe/BulkSubscribe.Example04.Tests/BulkSubscribe.Example04.Tests.csproj
```
