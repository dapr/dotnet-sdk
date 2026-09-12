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

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

public class HttpSubscriptionIntegrationTests
{
    private static async Task<(WebApplication App, HttpClient Client, HttpNotificationState NotifState, HttpBulkState BulkState)> CreateTestAppAsync(CancellationToken ct = default)
    {
        var notifState = new HttpNotificationState();
        var bulkState = new HttpBulkState();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(notifState);
        builder.Services.AddSingleton(bulkState);
        builder.Services.AddDaprMessaging()
            .AddDaprPubSub()
            .AddDaprSubscriber()
            .AddGeneratedSubscribers();
        builder.Services.AddRouting();

        var app = builder.Build();
        app.UseRouting();
        app.MapDaprHttpSubscriptions();

        await app.StartAsync(ct);
        var client = app.GetTestServer().CreateClient();
        return (app, client, notifState, bulkState);
    }

    [Fact]
    public async Task GetDaprSubscribe_ReturnsOnlyHttpTopicSubscriptions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client, _, _) = await CreateTestAppAsync(ct);
        await using var _ = app;

        var response = await client.GetAsync("/dapr/subscribe", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Equal(2, array.GetArrayLength());

        var notifSub = Assert.Single(array.EnumerateArray(), e => e.GetProperty("topic").GetString() == "integration-http-notifications");
        Assert.Equal("pubsub", notifSub.GetProperty("pubsubname").GetString());
        Assert.Equal("api/v1/notifications", notifSub.GetProperty("route").GetString());

        var bulkSub = Assert.Single(array.EnumerateArray(), e => e.GetProperty("topic").GetString() == "integration-http-bulk-items");
        Assert.Equal("pubsub", bulkSub.GetProperty("pubsubname").GetString());
        Assert.Equal("api/v1/bulk-items", bulkSub.GetProperty("route").GetString());
        var bulkConfig = bulkSub.GetProperty("bulkSubscribe");
        Assert.True(bulkConfig.GetProperty("enabled").GetBoolean());
        Assert.Equal(10, bulkConfig.GetProperty("maxMessagesCount").GetInt32());
        Assert.Equal(100, bulkConfig.GetProperty("maxAwaitDurationMs").GetInt32());

        // Ensure programmatic topics are NOT exposed via HTTP subscribe endpoint
        Assert.DoesNotContain(array.EnumerateArray(), e => e.GetProperty("topic").GetString() == "integration-orders");
    }

    [Fact]
    public async Task PostHttpTopicRoute_DispatchesCloudEventToGeneratedHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client, notifState, _) = await CreateTestAppAsync(ct);
        await using var _ = app;

        var cloudEvent = new
        {
            id = "notif-001",
            source = "test-service",
            type = "notification.v1",
            data = new { Message = "Server restarted", Severity = "Warning" }
        };

        var content = new StringContent(JsonSerializer.Serialize(cloudEvent), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/notifications", content, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        Assert.Equal("SUCCESS", doc.RootElement.GetProperty("status").GetString());

        var (received, context) = Assert.Single(notifState.Received);
        Assert.Equal("Server restarted", received.Message);
        Assert.Equal("Warning", received.Severity);
        Assert.Equal("notif-001", context.MessageId);
        Assert.Equal("integration-http-notifications", context.TopicName);
        Assert.Equal("pubsub", context.PubsubName);
    }

    [Fact]
    public async Task PostHttpTopicRoute_WithBase64Payload_DispatchesToHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client, notifState, _) = await CreateTestAppAsync(ct);
        await using var _ = app;

        var notification = new { Message = "Binary message", Severity = "Info" };
        var rawJson = JsonSerializer.Serialize(notification);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawJson));

        var cloudEvent = new
        {
            id = "notif-bin-01",
            data_base64 = base64
        };

        var content = new StringContent(JsonSerializer.Serialize(cloudEvent), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/notifications", content, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (received, context) = Assert.Single(notifState.Received);
        Assert.Equal("Binary message", received.Message);
        Assert.Equal("Info", received.Severity);
        Assert.Equal("notif-bin-01", context.MessageId);
    }

    [Fact]
    public async Task PostHttpTopicRoute_BulkSubscribe_DispatchesEntriesAndReturnsStatuses()
    {
        var ct = TestContext.Current.CancellationToken;
        var (app, client, _, bulkState) = await CreateTestAppAsync(ct);
        await using var _ = app;

        var bulkRequest = new
        {
            entries = new[]
            {
                new { entryId = "entry-101", data = new { ItemId = "item-alpha", Quantity = 7 } },
                new { entryId = "entry-102", data = new { ItemId = "item-beta", Quantity = 14 } }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(bulkRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/bulk-items", content, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var statuses = doc.RootElement.GetProperty("statuses");
        Assert.Equal(2, statuses.GetArrayLength());

        var statusMap = statuses.EnumerateArray().ToDictionary(
            s => s.GetProperty("entryId").GetString()!,
            s => s.GetProperty("status").GetString()!);

        Assert.Equal("SUCCESS", statusMap["entry-101"]);
        Assert.Equal("SUCCESS", statusMap["entry-102"]);

        Assert.Equal(2, bulkState.Received.Count);
        Assert.Contains(bulkState.Received, r => r.Item.ItemId == "item-alpha" && r.Item.Quantity == 7 && r.Context.MessageId == "entry-101");
        Assert.Contains(bulkState.Received, r => r.Item.ItemId == "item-beta" && r.Item.Quantity == 14 && r.Context.MessageId == "entry-102");
    }
}
