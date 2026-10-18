using BulkSubscribe.Example04;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr Messaging: discovers TelemetryBulkHandler with BulkSubscribe options
builder.Services.AddDaprMessaging();

var app = builder.Build();

// Endpoint to publish a single reading
app.MapPost("/publish/telemetry", async (DeviceTelemetry telemetry, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "telemetry", telemetry, ct);
    return Results.Accepted(value: new { Status = "Published", telemetry.DeviceId });
});

// Endpoint to bulk-publish a batch of telemetry readings
app.MapPost("/publish/telemetry-batch", async (List<DeviceTelemetry> readings, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    var response = await client.BulkPublishEventAsync("pubsub", "telemetry", readings, options: null, cancellationToken: ct);
    return Results.Accepted(value: new { Status = "BulkPublished", Total = readings.Count, Failed = response.FailedEntries.Count });
});

app.Run();
