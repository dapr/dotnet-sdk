using BulkSubscribe.Example04;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BulkSubscribe.Example04.Tests;

public class BulkSubscribeTests
{
    private readonly Mock<ILogger<TelemetryBulkHandler>> loggerMock = new();

    private static TopicContext CreateContext(string msgId = "bulk-msg-001") => new()
    {
        PubsubName = "pubsub",
        TopicName = "telemetry",
        MessageId = msgId,
        Metadata = new Dictionary<string, string>(),
        Headers = new Dictionary<string, string>()
    };

    [Fact]
    public async Task TelemetryBulkHandler_ValidReading_ReturnsSuccess()
    {
        var handler = new TelemetryBulkHandler(this.loggerMock.Object);
        var reading = new DeviceTelemetry("DEV-1", 23.5, 45.2, 1711800000000);
        var context = CreateContext();

        var action = await handler.HandleAsync(reading, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task TelemetryBulkHandler_InvalidTemperature_ReturnsDrop()
    {
        var handler = new TelemetryBulkHandler(this.loggerMock.Object);
        var reading = new DeviceTelemetry("DEV-2", -300.0, 50.0, 1711800000000);
        var context = CreateContext();

        var action = await handler.HandleAsync(reading, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task TelemetryBulkHandler_InvalidHumidity_ReturnsDrop()
    {
        var handler = new TelemetryBulkHandler(this.loggerMock.Object);
        var reading = new DeviceTelemetry("DEV-3", 20.0, 105.0, 1711800000000);
        var context = CreateContext();

        var action = await handler.HandleAsync(reading, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }
}
