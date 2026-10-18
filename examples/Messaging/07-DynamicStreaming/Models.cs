namespace DynamicStreaming.Example07;

/// <summary>
/// Tenant-specific lifecycle or business event.
/// </summary>
public record TenantEvent(
    string TenantId,
    string EventType,
    string Payload,
    DateTimeOffset Timestamp);
