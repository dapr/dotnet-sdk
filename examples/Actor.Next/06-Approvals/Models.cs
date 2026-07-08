namespace Dapr.Actors.Next.Examples.Approvals;

/// <summary>
/// A document-type template in the runtime catalog. The compiled actor hosts every one of these;
/// the behavior is the <c>Definition</c> supplied as data, not a compiled actor class.
/// </summary>
public sealed record DocumentTypeCard(string DocumentType, string Description);

/// <summary>
/// Payload for the <c>Submit</c> event. Carries the document details the settlement workflow needs.
/// </summary>
public sealed record SubmitDocument(
    string Requester,
    decimal Amount,
    IReadOnlyList<string> Parties,
    bool SimulateChargeFailure = false);

/// <summary>
/// Payload for the <c>Approve</c> / <c>Reject</c> events.
/// </summary>
public sealed record Decision(string Approver, string? Note = null);

/// <summary>
/// Input handed to the settlement workflow when a document is approved. Built by the
/// <c>StartSettlement</c> effect from the document's persisted state.
/// </summary>
public sealed record SettlementInput(
    string DocumentId,
    string DocumentType,
    string Requester,
    decimal Amount,
    IReadOnlyList<string> Parties,
    bool SimulateChargeFailure);

/// <summary>
/// Result of the settlement workflow.
/// </summary>
public sealed record SettlementResult(bool Settled, string FinalState);

/// <summary>
/// Input to the party-notification activity (the fan-out step).
/// </summary>
public sealed record PartyNotification(string DocumentId, string Party);

/// <summary>
/// Input to the charge/provision activity (the retried step).
/// </summary>
public sealed record ChargeRequest(string DocumentId, decimal Amount, bool SimulateFailure);

/// <summary>
/// Input to the compensation activity that undoes partial provisioning.
/// </summary>
public sealed record ReleaseRequest(string DocumentId, decimal Amount);

/// <summary>
/// Input to the activity that drives the interpreted document actor back to a settlement outcome.
/// </summary>
public sealed record DocumentSignal(string DocumentId, string EventName);
