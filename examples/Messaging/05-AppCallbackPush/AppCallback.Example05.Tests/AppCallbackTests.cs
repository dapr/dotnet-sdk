using AppCallback.Example05;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppCallback.Example05.Tests;

public class AppCallbackTests
{
    private readonly Mock<ILogger<PaymentProcessingHandler>> loggerMock = new();

    private static TopicContext CreateContext(string msgId = "payment-msg-001") => new()
    {
        PubsubName = "pubsub",
        TopicName = "payments",
        MessageId = msgId,
        Metadata = new Dictionary<string, string>(),
        Headers = new Dictionary<string, string>()
    };

    [Fact]
    public async Task PaymentProcessingHandler_ValidPayment_ReturnsSuccess()
    {
        var handler = new PaymentProcessingHandler(this.loggerMock.Object);
        var payment = new PaymentReceived("PAY-1", "CUST-1", 100.00m, "USD", "CreditCard");
        var context = CreateContext();

        var action = await handler.HandleAsync(payment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public async Task PaymentProcessingHandler_ZeroAmount_ReturnsDrop()
    {
        var handler = new PaymentProcessingHandler(this.loggerMock.Object);
        var payment = new PaymentReceived("PAY-2", "CUST-2", 0.00m, "USD", "PayPal");
        var context = CreateContext();

        var action = await handler.HandleAsync(payment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task PaymentProcessingHandler_NegativeAmount_ReturnsDrop()
    {
        var handler = new PaymentProcessingHandler(this.loggerMock.Object);
        var payment = new PaymentReceived("PAY-3", "CUST-3", -50.00m, "EUR", "CreditCard");
        var context = CreateContext();

        var action = await handler.HandleAsync(payment, context, CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, action);
    }
}
