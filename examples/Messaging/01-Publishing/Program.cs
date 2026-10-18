// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Text;
using Dapr.Messaging;
using Dapr.Messaging.Examples.Publishing;
using Dapr.Messaging.PublishSubscribe;

var builder = WebApplication.CreateBuilder(args);

// Register Dapr.Messaging: configures IDaprPublishSubscribeClient in DI
builder.Services.AddDaprMessaging();

var app = builder.Build();

const string PubsubName = "pubsub";
const string OrdersTopic = "orders";

// 1. Publish standard typed event
app.MapPost("/orders", async (OrderPlaced order, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    await client.PublishEventAsync(PubsubName, OrdersTopic, order, ct);
    return Results.Accepted($"/orders/{order.OrderId}", new { status = "Published", order.OrderId });
});

// 2. Publish with PublishOptions (custom metadata, CloudEvents attributes, TTL)
app.MapPost("/orders/priority", async (PriorityOrder order, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    var options = new PublishOptions
    {
        Metadata =
        {
            ["cloudevent.type"] = "priority.order",
            ["ttlInSeconds"] = "60"
        }
    };

    await client.PublishEventAsync(PubsubName, OrdersTopic, order, options, ct);
    return Results.Accepted($"/orders/{order.OrderId}", new { status = "Published Priority", order.OrderId });
});

// 3. Bulk publish multiple events in a single round-trip
app.MapPost("/orders/bulk", async (List<OrderBatchItem> orders, IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    var response = await client.BulkPublishEventAsync(PubsubName, OrdersTopic, orders, cancellationToken: ct);
    return Results.Ok(new
    {
        total = orders.Count,
        failed = response.FailedEntries.Count,
        statuses = response.FailedEntries
    });
});

// 4. Publish raw byte payload with specific content-type
app.MapPost("/orders/raw", async (IDaprPublishSubscribeClient client, CancellationToken ct) =>
{
    var rawPayload = Encoding.UTF8.GetBytes("RAW_PAYLOAD_DATA_SAMPLE");
    await client.PublishByteEventAsync(
        PubsubName,
        "raw-orders",
        rawPayload,
        dataContentType: "text/plain",
        cancellationToken: ct);

    return Results.Accepted(value: new { status = "Published Raw" });
});

app.Run();

public partial class Program { }
