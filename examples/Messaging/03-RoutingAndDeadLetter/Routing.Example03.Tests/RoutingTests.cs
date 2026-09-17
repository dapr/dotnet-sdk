using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Moq;
using Routing.Example03;
using Xunit;

namespace Routing.Example03.Tests;

public class RoutingTests
{
    private readonly Mock<ILogger<ExpressShippingHandler>> expressLogger = new();
    private readonly Mock<ILogger<InternationalShippingHandler>> intlLogger = new();
    private readonly Mock<ILogger<StandardShippingHandler>> standardLogger = new();
    private readonly Mock<ILogger<DeadLetterShipmentHandler>> dlqLogger = new();

    private static TopicContext CreateContext(string topic, string msgId = "msg-001") => new()
    {
        PubsubName = "pubsub",
        TopicName = topic,
        MessageId = msgId,
        Metadata = new Dictionary<string, string>(),
        Headers = new Dictionary<string, string>()
    };

    [Fact]
    public async Task ExpressShippingHandler_ValidExpressShipment_ReturnsSuccess()
    {
        var handler = new ExpressShippingHandler(this.expressLogger.Object);
        var shipment = new ShipmentPackage("SHIP-1", "express", "US", 1.5m, "user@example.com");
        var context = CreateContext("express-shipments");

        var action = await handler.HandleAsync(shipment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task ExpressShippingHandler_InvalidWeight_ReturnsDrop()
    {
        var handler = new ExpressShippingHandler(this.expressLogger.Object);
        var shipment = new ShipmentPackage("SHIP-2", "express", "US", -0.5m, "user@example.com");
        var context = CreateContext("express-shipments");

        var action = await handler.HandleAsync(shipment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task InternationalShippingHandler_NonUsDestination_ReturnsSuccess()
    {
        var handler = new InternationalShippingHandler(this.intlLogger.Object);
        var shipment = new ShipmentPackage("SHIP-3", "standard", "DE", 3.0m, "user@example.de");
        var context = CreateContext("international-shipments");

        var action = await handler.HandleAsync(shipment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task StandardShippingHandler_DomesticStandard_ReturnsSuccess()
    {
        var handler = new StandardShippingHandler(this.standardLogger.Object);
        var shipment = new ShipmentPackage("SHIP-4", "standard", "US", 2.0m, "user@example.com");
        var context = CreateContext("standard-shipments");

        var action = await handler.HandleAsync(shipment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task DeadLetterShipmentHandler_ReceivesDlqEvent_ReturnsSuccess()
    {
        var handler = new DeadLetterShipmentHandler(this.dlqLogger.Object);
        var shipment = new ShipmentPackage("SHIP-5", "express", "US", 0m, "bad@example.com");
        var context = CreateContext("deadletter-shipments");

        var action = await handler.HandleAsync(shipment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }
}
