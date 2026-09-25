using AppCallback.Example05;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr Messaging: discovers [DaprTopic] handlers and registers the gRPC AppCallbackService
builder.Services.AddDaprMessaging();

var app = builder.Build();

// Map Dapr messaging endpoints: exposes gRPC AppCallbackService for sidecar push calls
app.MapDaprMessaging();

// Helper endpoint to publish a payment event
app.MapPost("/publish/payment", async (PaymentReceived payment, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "payments", payment, ct);
    return Results.Accepted(value: new { Status = "Published", payment.PaymentId });
});

app.Run();
