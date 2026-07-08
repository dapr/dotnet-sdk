using System.Text.Json;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Interpreted;
using Dapr.Workflow;

namespace Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;

/// <summary>
/// Drives the interpreted document actor back to a settlement outcome. This is the workflow-to-actor
/// half of the composition: the workflow raises <c>SettlementCompleted</c> or <c>SettlementFailed</c> on
/// the document with no compile-time contract, using dynamic invocation. The document's turn runs after
/// the approving turn has completed, so this is a well-ordered follow-up, not a re-entrant call.
/// </summary>
public sealed partial class SignalDocumentActivity(IDynamicActorClient client, ILogger<SignalDocumentActivity> logger)
    : WorkflowActivity<DocumentSignal, object?>
{
    public override async Task<object?> RunAsync(WorkflowActivityContext context, DocumentSignal input)
    {
        LogSignalling(input.EventName, input.DocumentId);
        var evt = new InterpretedEvent(input.EventName, JsonSerializer.SerializeToElement(new { }));
        await client.InvokeAsync(ApprovalDefinitions.ActorType, input.DocumentId, "Raise", JsonSerializer.Serialize(evt));
        return null;
    }

    [LoggerMessage(LogLevel.Information, "Signalling {EventName} to document {DocumentId}")]
    private partial void LogSignalling(string eventName, string documentId);
}
