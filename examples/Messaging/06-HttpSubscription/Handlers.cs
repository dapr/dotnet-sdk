using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace HttpSubscription.Example06;

/// <summary>
/// Invoice processing topic handler using HTTP push delivery (DeliveryMode.Http).
/// Dapr discovers this subscription via GET /dapr/subscribe and delivers events via POST /api/events/invoices.
/// </summary>
[DaprTopic("pubsub", "invoices", Delivery = DeliveryMode.Http, Route = "api/events/invoices")]
public class InvoiceProcessingHandler : ITopicHandler<InvoiceGenerated>
{
    private readonly ILogger<InvoiceProcessingHandler> logger;

    public InvoiceProcessingHandler(ILogger<InvoiceProcessingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(InvoiceGenerated invoice, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "[HTTP PUSH] Received invoice {InvoiceId} for customer {CustomerId}: {Amount} {Currency}, Due: {DueDate:yyyy-MM-dd} (MsgId: {MsgId})",
            invoice.InvoiceId, invoice.CustomerId, invoice.TotalAmount, invoice.Currency, invoice.DueDate, context.MessageId);

        // Discard unprocessable invoices
        if (invoice.TotalAmount <= 0)
        {
            this.logger.LogWarning("Dropping invalid invoice {InvoiceId}: amount is non-positive", invoice.InvoiceId);
            return Task.FromResult(TopicResponseAction.Drop);
        }

        return Task.FromResult(TopicResponseAction.Success);
    }
}
