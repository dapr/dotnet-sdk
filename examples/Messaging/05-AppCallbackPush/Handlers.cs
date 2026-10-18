using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace AppCallback.Example05;

/// <summary>
/// Payment processing topic handler using gRPC push delivery (DeliveryMode.Programmatic).
/// Dapr sidecar initiates gRPC calls to the app's AppCallback service on inbound events.
/// </summary>
[DaprTopic("pubsub", "payments", Delivery = DeliveryMode.Programmatic)]
public class PaymentProcessingHandler : ITopicHandler<PaymentReceived>
{
    private readonly ILogger<PaymentProcessingHandler> logger;

    public PaymentProcessingHandler(ILogger<PaymentProcessingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(PaymentReceived payment, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "[gRPC PUSH] Received payment {PaymentId} of {Amount} {Currency} via {Method} (MsgId: {MsgId})",
            payment.PaymentId, payment.Amount, payment.Currency, payment.PaymentMethod, context.MessageId);

        // Discard unprocessable payments with non-positive amounts
        if (payment.Amount <= 0)
        {
            this.logger.LogWarning("Dropping payment {PaymentId}: amount is non-positive", payment.PaymentId);
            return Task.FromResult(TopicResponseAction.Drop);
        }

        return Task.FromResult(TopicResponseAction.Success);
    }
}
