using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Streaming.Example02;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr Messaging: discovers [DaprTopic] handlers at compile time, registers
// IDaprPublishSubscribeClient, and starts the StreamingSubscriberHostedService background service.
builder.Services.AddDaprMessaging();

var app = builder.Build();

// Helper endpoints to trigger sample events for testing the streaming subscriber
app.MapPost("/publish/order", async (OrderPlaced order, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "orders", order, ct);
    return Results.Accepted(value: new { Status = "Published", order.OrderId });
});

app.MapPost("/publish/inventory", async (InventoryReserved reservation, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "inventory-reserved", reservation, ct);
    return Results.Accepted(value: new { Status = "Published", reservation.ReservationId });
});

app.Run();
