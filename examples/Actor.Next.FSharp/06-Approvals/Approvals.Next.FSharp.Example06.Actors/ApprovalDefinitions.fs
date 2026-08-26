namespace Dapr.Actors.Next.Examples.Approvals

open System
open System.Collections.Generic
open System.Text.Json
open Dapr.Actors.Next.Interpreted

type ApprovalDefinitions =

    static member ActorType = "ApprovalDocument"

    static member RecordSubmissionEffect = "RecordSubmission"

    static member RecordDecisionEffect = "RecordDecision"

    static member NotifyManagerEffect = "NotifyManager"

    static member StartSettlementEffect = "StartSettlement"

    static member RecordSettlementFailureEffect = "RecordSettlementFailure"

    static member WithinApprovalLimitGuard = "WithinApprovalLimit"

    static member AutoApprovalLimit = 1000m

    static member val Catalog : IReadOnlyList<DocumentTypeCard> =
        upcast [|
            { DocumentType = "ExpenseReport"; Description = "Draft to Submitted to InReview; small amounts auto-approve, large ones escalate to a manager." }
            { DocumentType = "Contract"; Description = "Adds a LegalReview stage before InReview; otherwise the same approval and settlement tail." }
        |]
        with get

    static member ForType(documentType: string) : InterpretedMachineDefinition =
        match documentType with
        | "ExpenseReport" -> ApprovalDefinitions.ExpenseReport()
        | "Contract" -> ApprovalDefinitions.Contract()
        | _ -> raise (ArgumentException($"Unknown document type '{documentType}'.", "documentType"))

    static member ExpenseReport(?includeSettlementCompletion: bool, ?settlementEffect: string) : InterpretedMachineDefinition =
        let includeSettlementCompletion = defaultArg includeSettlementCompletion true
        let settlementEffect = defaultArg settlementEffect ApprovalDefinitions.StartSettlementEffect
        InterpretedMachineDefinition(
            DocumentVersion = 1,
            InitialState = "Draft",
            InitialData = ApprovalDefinitions.InitialData("ExpenseReport"),
            States = [|
                InterpretedStateDefinition(Name = "Draft")
                InterpretedStateDefinition(Name = "Submitted")
                InterpretedStateDefinition(Name = "InReview")
                InterpretedStateDefinition(Name = "Escalated")
                InterpretedStateDefinition(Name = "Approved", EntryEffects = [| settlementEffect |])
                InterpretedStateDefinition(Name = "Rejected", Terminal = true)
                InterpretedStateDefinition(Name = "Archived", Terminal = true)
                InterpretedStateDefinition(Name = "SettlementFailed", Terminal = true)
            |],
            Transitions = ApprovalDefinitions.ReviewTailWith(
                ApprovalDefinitions.Submit("Draft") :: ApprovalDefinitions.BeginReview("Submitted") :: ApprovalDefinitions.ReviewTail(includeSettlementCompletion)
                |> List.toArray)
        )

    static member Contract(?includeSettlementCompletion: bool) : InterpretedMachineDefinition =
        let includeSettlementCompletion = defaultArg includeSettlementCompletion true
        InterpretedMachineDefinition(
            DocumentVersion = 1,
            InitialState = "Draft",
            InitialData = ApprovalDefinitions.InitialData("Contract"),
            States = [|
                InterpretedStateDefinition(Name = "Draft")
                InterpretedStateDefinition(Name = "Submitted")
                InterpretedStateDefinition(Name = "LegalReview")
                InterpretedStateDefinition(Name = "InReview")
                InterpretedStateDefinition(Name = "Escalated")
                InterpretedStateDefinition(Name = "Approved", EntryEffects = [| ApprovalDefinitions.StartSettlementEffect |])
                InterpretedStateDefinition(Name = "Rejected", Terminal = true)
                InterpretedStateDefinition(Name = "Archived", Terminal = true)
                InterpretedStateDefinition(Name = "SettlementFailed", Terminal = true)
            |],
            Transitions = [|
                ApprovalDefinitions.Submit("Draft")
                ApprovalDefinitions.GoTo("Submitted", "BeginLegalReview", "LegalReview", [||])
                ApprovalDefinitions.GoTo("LegalReview", "CompleteLegalReview", "InReview", [||])
                yield! ApprovalDefinitions.ReviewTail(includeSettlementCompletion)
            |]
        )

    static member Verify(verifier: IInterpretedMachineVerifier, definition: InterpretedMachineDefinition) : InterpretedMachineVerificationResult =
        verifier.Verify(definition)

    static member private ReviewTailWith(transitions: InterpretedTransitionDefinition[]) : IReadOnlyList<InterpretedTransitionDefinition> =
        upcast transitions

    static member private ReviewTail(includeSettlementCompletion: bool) : InterpretedTransitionDefinition list =
        let transitions = ResizeArray<InterpretedTransitionDefinition>()
        transitions.Add(
            InterpretedTransitionDefinition(
                Source = "InReview",
                Event = "Approve",
                Branches = [|
                    InterpretedBranchDefinition(
                        Guards = [| ApprovalDefinitions.WithinApprovalLimitGuard |],
                        Target = "Approved",
                        Effects = [| ApprovalDefinitions.RecordDecisionEffect |]
                    )
                    InterpretedBranchDefinition(
                        Otherwise = true,
                        Target = "Escalated",
                        Effects = [| ApprovalDefinitions.NotifyManagerEffect |]
                    )
                |]
            )
        )
        transitions.Add(ApprovalDefinitions.GoTo("InReview", "Reject", "Rejected", [| ApprovalDefinitions.RecordDecisionEffect |]))
        transitions.Add(ApprovalDefinitions.GoTo("Escalated", "Approve", "Approved", [| ApprovalDefinitions.RecordDecisionEffect |]))
        transitions.Add(ApprovalDefinitions.GoTo("Escalated", "Reject", "Rejected", [| ApprovalDefinitions.RecordDecisionEffect |]))

        if includeSettlementCompletion then
            transitions.Add(ApprovalDefinitions.GoTo("Approved", "SettlementCompleted", "Archived", [||]))
            transitions.Add(ApprovalDefinitions.GoTo("Approved", "SettlementFailed", "SettlementFailed", [| ApprovalDefinitions.RecordSettlementFailureEffect |]))

        transitions |> Seq.toList

    static member private Submit(source: string) : InterpretedTransitionDefinition =
        InterpretedTransitionDefinition(
            Source = source,
            Event = "Submit",
            Branches = [| InterpretedBranchDefinition(Otherwise = true, Target = "Submitted", Effects = [| ApprovalDefinitions.RecordSubmissionEffect |]) |]
        )

    static member private BeginReview(source: string) : InterpretedTransitionDefinition =
        ApprovalDefinitions.GoTo(source, "BeginReview", "InReview", [||])

    static member private GoTo(source: string, eventName: string, target: string, effects: string[]) : InterpretedTransitionDefinition =
        InterpretedTransitionDefinition(
            Source = source,
            Event = eventName,
            Branches = [| InterpretedBranchDefinition(Otherwise = true, Target = target, Effects = effects) |]
        )

    static member private InitialData(documentType: string) : IReadOnlyDictionary<string, JsonElement> =
        let dict = Dictionary<string, JsonElement>(StringComparer.Ordinal)
        dict.["documentType"] <- JsonSerializer.SerializeToElement(documentType)
        upcast dict
