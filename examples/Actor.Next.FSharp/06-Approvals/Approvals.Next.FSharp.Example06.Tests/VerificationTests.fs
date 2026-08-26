namespace Approvals.Next.FSharp.Example06.Tests

open System
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Interpreted
open Dapr.Actors.Next.Examples.Approvals
open Dapr.Workflow
open Xunit

type VerificationTests() =

    static member private Expense = ActorId.Create("exp-1")

    member private _.Registry() : ApprovalCapabilityRegistry =
        ApprovalCapabilityRegistry(new RecordingWorkflowClient())

    [<Fact>]
    member this.Well_formed_definition_passes_verification() =
        let verifier = InterpretedMachineVerifier(this.Registry() :> ICapabilityRegistry)
        Assert.True(ApprovalDefinitions.Verify(verifier, ApprovalDefinitions.ExpenseReport()).IsValid)
        Assert.True(ApprovalDefinitions.Verify(verifier, ApprovalDefinitions.Contract()).IsValid)

    [<Fact>]
    member this.Verification_rejects_definition_that_can_strand_an_approved_document() =
        let verifier = InterpretedMachineVerifier(this.Registry() :> ICapabilityRegistry)
        let stranded = ApprovalDefinitions.ExpenseReport(includeSettlementCompletion = false)

        let result = ApprovalDefinitions.Verify(verifier, stranded)

        Assert.False(result.IsValid)
        let containsDeadEnd =
            result.Defects
            |> Seq.exists (fun d -> d.Contains("State 'Approved' is a dead end", StringComparison.Ordinal))
        Assert.True(containsDeadEnd)

    [<Fact>]
    member this.Definition_referencing_an_unregistered_effect_is_rejected_before_rollout() = task {
        let registry = this.Registry()
        let deployer = InterpretedMachineDeployer(
            InterpretedMachineVerifier(registry :> ICapabilityRegistry) :> IInterpretedMachineVerifier,
            InMemoryInterpretedMachineStore() :> IInterpretedMachineStore)
        let definition = ApprovalDefinitions.ExpenseReport(settlementEffect = "UnknownSettlement")

        let capRegistry = registry :> ICapabilityRegistry
        let mutable effect = Unchecked.defaultof<IActorEffect>
        Assert.False(capRegistry.TryGetEffect("UnknownSettlement", &effect))

        let! ex = Assert.ThrowsAsync<InvalidOperationException>(fun () ->
            deployer.DeployAsync(ApprovalDefinitions.ActorType, VerificationTests.Expense, definition).AsTask())
        Assert.Contains("Effect 'UnknownSettlement'", ex.Message, StringComparison.Ordinal)
    }
