namespace AppCallback.Example05;

/// <summary>
/// Financial transaction event delivered via gRPC AppCallback push.
/// </summary>
public record PaymentReceived(
    string PaymentId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod);
