using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace Routing.Example03;

/// <summary>
/// Handles high-priority express shipments.
/// Matched via CEL rule: event.data.PriorityTier == 'express' (Priority = 1).
/// Undeliverable or dropped messages are routed to the configured DeadLetterTopic ("deadletter-shipments").
/// </summary>
[DaprTopic("pubsub", "express-shipments", Match = "event.data.PriorityTier == 'express'", Priority = 1, DeadLetterTopic = "deadletter-shipments")]
[DaprTopicMetadata("routingType", "express-tier")]
public class ExpressShippingHandler : ITopicHandler<ShipmentPackage>
{
    private readonly ILogger<ExpressShippingHandler> logger;

    public ExpressShippingHandler(ILogger<ExpressShippingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "[EXPRESS ROUTE] Processing urgent shipment {ShipmentId} to {Country} ({Weight}kg)",
            shipment.ShipmentId, shipment.DestinationCountry, shipment.WeightKg);

        if (shipment.WeightKg <= 0)
        {
            this.logger.LogWarning("Dropping express shipment {ShipmentId}: invalid weight", shipment.ShipmentId);
            return Task.FromResult(TopicResponseAction.Drop);
        }

        return Task.FromResult(TopicResponseAction.Success);
    }
}

/// <summary>
/// Handles international shipments (destination outside the US).
/// Matched via CEL rule: event.data.DestinationCountry != 'US' (Priority = 2).
/// </summary>
[DaprTopic("pubsub", "international-shipments", Match = "event.data.DestinationCountry != 'US'", Priority = 1, DeadLetterTopic = "deadletter-shipments")]
[DaprTopicMetadata("routingType", "international-customs")]
public class InternationalShippingHandler : ITopicHandler<ShipmentPackage>
{
    private readonly ILogger<InternationalShippingHandler> logger;

    public InternationalShippingHandler(ILogger<InternationalShippingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "[INTERNATIONAL ROUTE] Preparing customs clearance for {ShipmentId} to {Country}",
            shipment.ShipmentId, shipment.DestinationCountry);

        return Task.FromResult(TopicResponseAction.Success);
    }
}

/// <summary>
/// Fallback default handler for standard domestic shipments.
/// </summary>
[DaprTopic("pubsub", "standard-shipments", Priority = 10, DeadLetterTopic = "deadletter-shipments")]
[DaprTopicMetadata("routingType", "standard-domestic")]
public class StandardShippingHandler : ITopicHandler<ShipmentPackage>
{
    private readonly ILogger<StandardShippingHandler> logger;

    public StandardShippingHandler(ILogger<StandardShippingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "[STANDARD ROUTE] Processing standard domestic shipment {ShipmentId} to {Country}",
            shipment.ShipmentId, shipment.DestinationCountry);

        return Task.FromResult(TopicResponseAction.Success);
    }
}

/// <summary>
/// Handles messages forwarded to the dead-letter topic when a handler returns TopicResponseAction.Drop
/// or exhausts redelivery attempts.
/// </summary>
[DaprTopic("pubsub", "deadletter-shipments")]
public class DeadLetterShipmentHandler : ITopicHandler<ShipmentPackage>
{
    private readonly ILogger<DeadLetterShipmentHandler> logger;

    public DeadLetterShipmentHandler(ILogger<DeadLetterShipmentHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(ShipmentPackage shipment, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogError(
            "[DEAD LETTER QUEUE] Received rejected shipment {ShipmentId} from topic {Topic}. Context MsgId: {MsgId}",
            shipment.ShipmentId, context.TopicName, context.MessageId);

        // Acknowledge receipt in DLQ monitoring service
        return Task.FromResult(TopicResponseAction.Success);
    }
}
