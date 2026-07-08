using System.Text.Json;
using Dapr.Actors.Next.Interpreted;

namespace Dapr.Actors.Next.Examples.Approvals;

/// <summary>
/// The document-type definitions, authored as data rather than compiled. A single compiled
/// <see cref="InterpretedStateMachineActor"/> (registered once under <see cref="ActorType"/>) runs
/// every one of these; onboarding a document deploys its type's definition at runtime, so a new
/// document type is configuration rather than a code deploy.
/// </summary>
public static class ApprovalDefinitions
{
    /// <summary>
    /// The one compiled interpreted actor type that hosts every document type.
    /// </summary>
    public const string ActorType = "ApprovalDocument";

    // Vetted capability names resolved by the ApprovalCapabilityRegistry.
    public const string RecordSubmissionEffect = "RecordSubmission";
    public const string RecordDecisionEffect = "RecordDecision";
    public const string NotifyManagerEffect = "NotifyManager";
    public const string StartSettlementEffect = "StartSettlement";
    public const string RecordSettlementFailureEffect = "RecordSettlementFailure";
    public const string WithinApprovalLimitGuard = "WithinApprovalLimit";

    /// <summary>
    /// The runtime catalog of onboardable document types.
    /// </summary>
    public static IReadOnlyList<DocumentTypeCard> Catalog { get; } =
    [
        new("ExpenseReport", "Draft to Submitted to InReview; small amounts auto-approve, large ones escalate to a manager."),
        new("Contract", "Adds a LegalReview stage before InReview; otherwise the same approval and settlement tail."),
    ];

    /// <summary>
    /// Builds the definition document for a document type, or throws for an unknown type.
    /// </summary>
    public static InterpretedMachineDefinition ForType(string documentType) => documentType switch
    {
        "ExpenseReport" => ExpenseReport(),
        "Contract" => Contract(),
        _ => throw new ArgumentException($"Unknown document type '{documentType}'.", nameof(documentType)),
    };

    /// <summary>
    /// The expense-report machine. The optional flag lets a test build a deliberately-stranded machine
    /// (an <c>Approved</c> state with no way out), and the effect name is a parameter so a test can
    /// reference an unregistered effect.
    /// </summary>
    public static InterpretedMachineDefinition ExpenseReport(
        bool includeSettlementCompletion = true,
        string settlementEffect = StartSettlementEffect) =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Draft",
            InitialData = InitialData("ExpenseReport"),
            States =
            [
                new InterpretedStateDefinition { Name = "Draft" },
                new InterpretedStateDefinition { Name = "Submitted" },
                new InterpretedStateDefinition { Name = "InReview" },
                new InterpretedStateDefinition { Name = "Escalated" },
                new InterpretedStateDefinition { Name = "Approved", EntryEffects = [settlementEffect] },
                new InterpretedStateDefinition { Name = "Rejected", Terminal = true },
                new InterpretedStateDefinition { Name = "Archived", Terminal = true },
                new InterpretedStateDefinition { Name = "SettlementFailed", Terminal = true },
            ],
            Transitions =
            [
                Submit("Draft"),
                BeginReview("Submitted"),
                .. ReviewTail(includeSettlementCompletion),
            ],
        };

    /// <summary>
    /// The contract machine: identical approval and settlement tail, but with a mandatory legal-review
    /// stage inserted before the standard review. Demonstrates that different document types are
    /// genuinely different machines while sharing one compiled actor.
    /// </summary>
    public static InterpretedMachineDefinition Contract(bool includeSettlementCompletion = true) =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Draft",
            InitialData = InitialData("Contract"),
            States =
            [
                new InterpretedStateDefinition { Name = "Draft" },
                new InterpretedStateDefinition { Name = "Submitted" },
                new InterpretedStateDefinition { Name = "LegalReview" },
                new InterpretedStateDefinition { Name = "InReview" },
                new InterpretedStateDefinition { Name = "Escalated" },
                new InterpretedStateDefinition { Name = "Approved", EntryEffects = [StartSettlementEffect] },
                new InterpretedStateDefinition { Name = "Rejected", Terminal = true },
                new InterpretedStateDefinition { Name = "Archived", Terminal = true },
                new InterpretedStateDefinition { Name = "SettlementFailed", Terminal = true },
            ],
            Transitions =
            [
                Submit("Draft"),
                Goto("Submitted", "BeginLegalReview", "LegalReview"),
                Goto("LegalReview", "CompleteLegalReview", "InReview"),
                .. ReviewTail(includeSettlementCompletion),
            ],
        };

    /// <summary>
    /// Verifies a definition against the vetted capability registry, exactly as deployment does.
    /// </summary>
    public static InterpretedMachineVerificationResult Verify(
        IInterpretedMachineVerifier verifier,
        InterpretedMachineDefinition definition) =>
        verifier.Verify(definition);

    // The shared review/approve/settle tail: InReview -> (auto-approve | escalate) -> Approved -> settle.
    private static IReadOnlyList<InterpretedTransitionDefinition> ReviewTail(bool includeSettlementCompletion)
    {
        var transitions = new List<InterpretedTransitionDefinition>
        {
            new()
            {
                Source = "InReview",
                Event = "Approve",
                Branches =
                [
                    // Small amounts clear the auto-approval limit and go straight to Approved.
                    new InterpretedBranchDefinition
                    {
                        Guards = [WithinApprovalLimitGuard],
                        Target = "Approved",
                        Effects = [RecordDecisionEffect],
                    },
                    // Everything else needs a manager, so it escalates instead.
                    new InterpretedBranchDefinition
                    {
                        Otherwise = true,
                        Target = "Escalated",
                        Effects = [NotifyManagerEffect],
                    },
                ],
            },
            Goto("InReview", "Reject", "Rejected", RecordDecisionEffect),
            Goto("Escalated", "Approve", "Approved", RecordDecisionEffect),
            Goto("Escalated", "Reject", "Rejected", RecordDecisionEffect),
        };

        if (includeSettlementCompletion)
        {
            // The settlement workflow drives one of these back into the actor when it finishes.
            transitions.Add(Goto("Approved", "SettlementCompleted", "Archived"));
            transitions.Add(Goto("Approved", "SettlementFailed", "SettlementFailed", RecordSettlementFailureEffect));
        }

        return transitions;
    }

    private static InterpretedTransitionDefinition Submit(string source) => new()
    {
        Source = source,
        Event = "Submit",
        Branches = [new InterpretedBranchDefinition { Otherwise = true, Target = "Submitted", Effects = [RecordSubmissionEffect] }],
    };

    private static InterpretedTransitionDefinition BeginReview(string source) =>
        Goto(source, "BeginReview", "InReview");

    private static InterpretedTransitionDefinition Goto(string source, string @event, string target, params string[] effects) => new()
    {
        Source = source,
        Event = @event,
        Branches = [new InterpretedBranchDefinition { Otherwise = true, Target = target, Effects = effects }],
    };

    private static IReadOnlyDictionary<string, JsonElement> InitialData(string documentType) =>
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["documentType"] = JsonSerializer.SerializeToElement(documentType),
        };
}
