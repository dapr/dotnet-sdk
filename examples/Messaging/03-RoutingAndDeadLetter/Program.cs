using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Routing.Example03;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr Messaging: discovers handlers and emitted dispatchers with CEL routing and DLQ config
builder.Services.AddDaprMessaging();

var app = builder.Build();

// Endpoint to publish shipment events for testing content-based routing
app.MapPost("/publish/express", async (ShipmentPackage shipment, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "express-shipments", shipment, ct);
    return Results.Accepted(value: new { Status = "Published", Topic = "express-shipments", shipment.ShipmentId });
});

app.MapPost("/publish/international", async (ShipmentPackage shipment, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "international-shipments", shipment, ct);
    return Results.Accepted(value: new { Status = "Published", Topic = "international-shipments", shipment.ShipmentId });
});

app.MapPost("/publish/standard", async (ShipmentPackage shipment, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "standard-shipments", shipment, ct);
    return Results.Accepted(value: new { Status = "Published", Topic = "standard-shipments", shipment.ShipmentId });
});

app.Run();
