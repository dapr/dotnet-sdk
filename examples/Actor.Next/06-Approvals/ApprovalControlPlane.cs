using System.Text.Json;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Interpreted;

namespace Dapr.Actors.Next.Examples.Approvals;

/// <summary>
/// Drives documents generically by type, id, and event, with no compile-time document contract. It
/// discovers what the hosted actor accepts through <see cref="IActorRegistry"/> and sends events through
/// <see cref="IDynamicActorClient"/> — the same weakly-typed calling path a cross-language or gateway
/// caller would use.
/// </summary>
public sealed class ApprovalControlPlane(IActorRegistry registry, IDynamicActorClient client)
{
    public IReadOnlyList<string> HostedActorTypes() =>
        registry.Actors.Select(actor => actor.ActorType).Order(StringComparer.Ordinal).ToArray();

    public Task<string?> SubmitAsync(string documentId, SubmitDocument submission, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentId, "Submit", submission, cancellationToken);

    public Task<string?> BeginReviewAsync(string documentId, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentId, "BeginReview", new { }, cancellationToken);

    public Task<string?> BeginLegalReviewAsync(string documentId, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentId, "BeginLegalReview", new { }, cancellationToken);

    public Task<string?> CompleteLegalReviewAsync(string documentId, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentId, "CompleteLegalReview", new { }, cancellationToken);

    public Task<string?> ApproveAsync(string documentId, Decision decision, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentId, "Approve", decision, cancellationToken);

    public Task<string?> RejectAsync(string documentId, Decision decision, CancellationToken cancellationToken = default) =>
        RaiseAsync(documentId, "Reject", decision, cancellationToken);

    /// <summary>
    /// Purges a document's persisted state so it starts fresh. Called by onboarding so re-onboarding a
    /// document clears any prior run; safe for a document that was never onboarded (a no-op).
    /// </summary>
    public Task ResetAsync(string documentId, CancellationToken cancellationToken = default) =>
        client.InvokeAsync(ApprovalDefinitions.ActorType, documentId, "Reset", "{}", cancellationToken);

    private Task<string?> RaiseAsync<TPayload>(string documentId, string eventName, TPayload payload, CancellationToken cancellationToken)
    {
        var evt = new InterpretedEvent(eventName, JsonSerializer.SerializeToElement(payload));
        return client.InvokeAsync(ApprovalDefinitions.ActorType, documentId, "Raise", JsonSerializer.Serialize(evt), cancellationToken);
    }
}
