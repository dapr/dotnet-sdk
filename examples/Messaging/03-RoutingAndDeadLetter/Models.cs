namespace Routing.Example03;

/// <summary>
/// Represents a shipment package event payload subject to content-based routing rules.
/// </summary>
public record ShipmentPackage(
    string ShipmentId,
    string PriorityTier,
    string DestinationCountry,
    decimal WeightKg,
    string RecipientEmail);

/// <summary>
/// Represents a failed or rejected shipment event routed to the dead-letter topic.
/// </summary>
public record DeadLetterShipment(
    string ShipmentId,
    string Reason,
    string SourceTopic);
