using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace Streaming.Example02;

/// <summary>
/// Processes incoming orders delivered via streaming pull subscription.
/// Demonstrates explicit acknowledgement control using <see cref="TopicResponseAction"/>.
/// </summary>
[DaprTopic("pubsub", "orders", Delivery = DeliveryMode.Streaming)]
public class OrderProcessingHandler : ITopicHandler<OrderPlaced>
{
    private readonly ILogger<OrderProcessingHandler> logger;

    public OrderProcessingHandler(ILogger<OrderProcessingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(OrderPlaced order, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Received order {OrderId} for {Customer} (SKU: {Sku}, Qty: {Qty}, Amount: {Amount}) via topic {Topic}",
            order.OrderId, order.CustomerId, order.ItemSku, order.Quantity, order.Amount, context.TopicName);

        // Validation: Unprocessable / invalid orders should be dropped to avoid poison message loops
        if (order.Quantity <= 0 || string.IsNullOrWhiteSpace(order.ItemSku))
        {
            this.logger.LogWarning("Dropping invalid order {OrderId}: quantity is non-positive or SKU is empty", order.OrderId);
            return Task.FromResult(TopicResponseAction.Drop);
        }

        // Transient failure simulation: If SKU indicates out-of-stock / lock conflict, ask Dapr to retry redelivery
        if (order.ItemSku.StartsWith("RETRY-", StringComparison.OrdinalIgnoreCase))
        {
            this.logger.LogWarning("Transient inventory lock on SKU {Sku}. Requesting redelivery (Retry)", order.ItemSku);
            return Task.FromResult(TopicResponseAction.Retry);
        }

        // Successfully processed order
        this.logger.LogInformation("Successfully processed order {OrderId}", order.OrderId);
        return Task.FromResult(TopicResponseAction.Success);
    }
}

/// <summary>
/// Processes inventory reservation events.
/// Demonstrates reading CloudEvent and metadata properties from <see cref="TopicContext"/>.
/// </summary>
[DaprTopic("pubsub", "inventory-reserved", Delivery = DeliveryMode.Streaming)]
public class InventoryReservedHandler : ITopicHandler<InventoryReserved>
{
    private readonly ILogger<InventoryReservedHandler> logger;

    public InventoryReservedHandler(ILogger<InventoryReservedHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(InventoryReserved message, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Reserved {Qty} of SKU {Sku} for order {OrderId} (MsgId: {MessageId}, PubSub: {PubsubName})",
            message.QuantityReserved, message.ItemSku, message.OrderId, context.MessageId, context.PubsubName);

        return Task.FromResult(TopicResponseAction.Success);
    }
}
