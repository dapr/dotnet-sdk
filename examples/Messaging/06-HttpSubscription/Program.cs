using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using HttpSubscription.Example06;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr Messaging: discovers HTTP [DaprTopic] handlers
builder.Services.AddDaprMessaging();

var app = builder.Build();

// Map Dapr messaging endpoints: exposes GET /dapr/subscribe and POST /api/events/invoices
app.MapDaprMessaging();

// Helper endpoint to publish an invoice event
app.MapPost("/publish/invoice", async (InvoiceGenerated invoice, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "invoices", invoice, ct);
    return Results.Accepted(value: new { Status = "Published", invoice.InvoiceId });
});

app.Run();
