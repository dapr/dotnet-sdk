namespace Streaming.Example02;

/// <summary>
/// Represents a customer order event payload.
/// </summary>
public record OrderPlaced(string OrderId, string CustomerId, decimal Amount, string ItemSku, int Quantity);

/// <summary>
/// Represents an inventory reservation event payload.
/// </summary>
public record InventoryReserved(string ReservationId, string OrderId, string ItemSku, int QuantityReserved);
