using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using DynamicStreaming.Example07;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr Pub/Sub client and the dynamic subscriber background worker
builder.Services.AddDaprPubSubClient();
builder.Services.AddHostedService<DynamicSubscriberWorker>();

var app = builder.Build();

// Helper endpoint to publish a tenant event
app.MapPost("/publish/tenant-event", async (TenantEvent tenantEvent, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync("pubsub", "tenant-events", tenantEvent, ct);
    return Results.Accepted(value: new { Status = "Published", tenantEvent.TenantId, tenantEvent.EventType });
});

app.Run();
