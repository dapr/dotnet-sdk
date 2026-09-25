using System.Text;
using System.Text.Json;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using DynamicStreaming.Example07;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DynamicStreaming.Example07.Tests;

public class DynamicStreamingTests
{
    private readonly Mock<ILogger<DynamicSubscriberWorker>> loggerMock = new();

    private static TopicMessage CreateTopicMessage(object data, string topic = "tenant-events")
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data);
        return new TopicMessage(
            Id: "dyn-msg-001",
            Source: "test-source",
            Type: "com.dapr.tenant.event",
            SpecVersion: "1.0",
            DataContentType: "application/json",
            Topic: topic,
            PubSubName: "pubsub")
        {
            Data = json
        };
    }

    [Fact]
    public async Task DynamicHandler_ValidTenantEvent_ReturnsSuccess()
    {
        var worker = new DynamicSubscriberWorker(null!, this.loggerMock.Object);
        var @event = new TenantEvent("TENANT-1", "UserCreated", "{}", DateTimeOffset.UtcNow);
        var message = CreateTopicMessage(@event);

        var action = await worker.HandleDynamicMessageAsync(message, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task DynamicHandler_RetryEventType_ReturnsRetry()
    {
        var worker = new DynamicSubscriberWorker(null!, this.loggerMock.Object);
        var @event = new TenantEvent("TENANT-2", "RETRY-SyncExternalDatabase", "{}", DateTimeOffset.UtcNow);
        var message = CreateTopicMessage(@event);

        var action = await worker.HandleDynamicMessageAsync(message, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Retry, action);
    }

    [Fact]
    public async Task DynamicHandler_EmptyTenantId_ReturnsDrop()
    {
        var worker = new DynamicSubscriberWorker(null!, this.loggerMock.Object);
        var @event = new TenantEvent("", "UserCreated", "{}", DateTimeOffset.UtcNow);
        var message = CreateTopicMessage(@event);

        var action = await worker.HandleDynamicMessageAsync(message, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task DynamicHandler_MalformedJson_ReturnsDrop()
    {
        var worker = new DynamicSubscriberWorker(null!, this.loggerMock.Object);
        var invalidBytes = Encoding.UTF8.GetBytes("{ not-valid-json }");
        var message = new TopicMessage(
            Id: "dyn-msg-err",
            Source: "test",
            Type: "test",
            SpecVersion: "1.0",
            DataContentType: "application/json",
            Topic: "tenant-events",
            PubSubName: "pubsub")
        {
            Data = invalidBytes
        };

        var action = await worker.HandleDynamicMessageAsync(message, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }
}
