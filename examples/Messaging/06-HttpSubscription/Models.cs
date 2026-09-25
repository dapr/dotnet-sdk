namespace HttpSubscription.Example06;

/// <summary>
/// Invoice generation event delivered via HTTP push callback.
/// </summary>
public record InvoiceGenerated(
    string InvoiceId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset DueDate);
