using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Moq;
using Streaming.Example02;
using Xunit;

namespace Streaming.Example02.Tests;

public class StreamingTests
{
    private readonly Mock<ILogger<OrderProcessingHandler>> orderLoggerMock = new();
    private readonly Mock<ILogger<InventoryReservedHandler>> inventoryLoggerMock = new();

    private static TopicContext CreateContext(string topic, string msgId = "test-msg-001") => new()
    {
        PubsubName = "pubsub",
        TopicName = topic,
        MessageId = msgId,
        Metadata = new Dictionary<string, string>(),
        Headers = new Dictionary<string, string>()
    };

    [Fact]
    public async Task OrderProcessingHandler_ValidOrder_ReturnsSuccess()
    {
        var handler = new OrderProcessingHandler(this.orderLoggerMock.Object);
        var order = new OrderPlaced("ORD-1", "CUST-1", 99.95m, "SKU-ABC", 2);
        var context = CreateContext("orders");

        var action = await handler.HandleAsync(order, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task OrderProcessingHandler_InvalidQuantity_ReturnsDrop()
    {
        var handler = new OrderProcessingHandler(this.orderLoggerMock.Object);
        var order = new OrderPlaced("ORD-2", "CUST-1", 0m, "SKU-ABC", 0);
        var context = CreateContext("orders");

        var action = await handler.HandleAsync(order, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task OrderProcessingHandler_RetrySku_ReturnsRetry()
    {
        var handler = new OrderProcessingHandler(this.orderLoggerMock.Object);
        var order = new OrderPlaced("ORD-3", "CUST-2", 49.99m, "RETRY-LIMITED-EDITION", 1);
        var context = CreateContext("orders");

        var action = await handler.HandleAsync(order, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Retry, action);
    }

    [Fact]
    public async Task InventoryReservedHandler_ValidEvent_ReturnsSuccess()
    {
        var handler = new InventoryReservedHandler(this.inventoryLoggerMock.Object);
        var reservation = new InventoryReserved("RES-101", "ORD-1", "SKU-ABC", 2);
        var context = CreateContext("inventory-reserved");

        var action = await handler.HandleAsync(reservation, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }
}
