using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using HttpSubscription.Example06;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HttpSubscription.Example06.Tests;

public class HttpSubscriptionTests
{
    private readonly Mock<ILogger<InvoiceProcessingHandler>> loggerMock = new();

    private static TopicContext CreateContext(string msgId = "inv-msg-001") => new()
    {
        PubsubName = "pubsub",
        TopicName = "invoices",
        MessageId = msgId,
        Metadata = new Dictionary<string, string>(),
        Headers = new Dictionary<string, string>()
    };

    [Fact]
    public async Task InvoiceProcessingHandler_ValidInvoice_ReturnsSuccess()
    {
        var handler = new InvoiceProcessingHandler(this.loggerMock.Object);
        var invoice = new InvoiceGenerated("INV-101", "CUST-A", 500m, "USD", DateTimeOffset.UtcNow.AddDays(30));
        var context = CreateContext();

        var action = await handler.HandleAsync(invoice, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task InvoiceProcessingHandler_ZeroTotalAmount_ReturnsDrop()
    {
        var handler = new InvoiceProcessingHandler(this.loggerMock.Object);
        var invoice = new InvoiceGenerated("INV-102", "CUST-B", 0m, "USD", DateTimeOffset.UtcNow.AddDays(30));
        var context = CreateContext();

        var action = await handler.HandleAsync(invoice, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task InvoiceProcessingHandler_NegativeTotalAmount_ReturnsDrop()
    {
        var handler = new InvoiceProcessingHandler(this.loggerMock.Object);
        var invoice = new InvoiceGenerated("INV-103", "CUST-C", -25m, "USD", DateTimeOffset.UtcNow.AddDays(30));
        var context = CreateContext();

        var action = await handler.HandleAsync(invoice, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }
}
