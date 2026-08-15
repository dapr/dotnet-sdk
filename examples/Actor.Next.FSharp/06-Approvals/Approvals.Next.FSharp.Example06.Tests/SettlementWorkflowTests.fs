namespace Approvals.Next.FSharp.Example06.Tests

open System.Collections.Generic
open System.Threading.Tasks
open Dapr.Actors.Next.Examples.Approvals
open Dapr.Workflow
open Xunit

type SettlementWorkflowTests() =

    [<Fact>]
    member this.Successful_charge_notifies_every_party_then_archives_the_document() = task {
        let input : SettlementInput = {
            DocumentId = "exp-1"
            DocumentType = "ExpenseReport"
            Requester = "alice"
            Amount = 250m
            Parties = ([| "finance"; "alice" |] :> IReadOnlyList<string>)
            SimulateChargeFailure = false
        }
        let context = TestWorkflowContext()
        context.SetActivityResult(typeof<ChargeOrProvisionActivity>.Name, fun _ -> Task.CompletedTask)

        let workflow = SettlementWorkflow()
        let! result = workflow.RunAsync(context, input)

        Assert.True(result.Settled)
        Assert.Equal("Archived", result.FinalState)

        Assert.Equal(2, context.GetCallCount(typeof<NotifyPartiesActivity>.Name))
        Assert.Equal(1, context.GetCallCount(typeof<SignalDocumentActivity>.Name))
        Assert.Equal(0, context.GetCallCount(typeof<ReleaseReservationActivity>.Name))

        let signals = context.GetCalls<DocumentSignal>(typeof<SignalDocumentActivity>.Name)
        Assert.True(signals |> List.exists (fun s -> s.EventName = "SettlementCompleted"))
    }

    [<Fact>]
    member this.Charge_that_exhausts_its_retries_compensates_and_fails_the_document() = task {
        let input : SettlementInput = {
            DocumentId = "exp-2"
            DocumentType = "ExpenseReport"
            Requester = "carol"
            Amount = 8500m
            Parties = ([| "finance"; "carol"; "vendor" |] :> IReadOnlyList<string>)
            SimulateChargeFailure = true
        }
        let context = TestWorkflowContext()
        context.SetActivityResult(typeof<ChargeOrProvisionActivity>.Name, fun _ ->
            Task.FromException(WorkflowTaskFailedException(
                "charge failed",
                WorkflowTaskFailureDetails("Declined", "Charge was declined"))))

        let workflow = SettlementWorkflow()
        let! result = workflow.RunAsync(context, input)

        Assert.False(result.Settled)
        Assert.Equal("SettlementFailed", result.FinalState)

        Assert.Equal(1, context.GetCallCount(typeof<ReleaseReservationActivity>.Name))
        Assert.Equal(1, context.GetCallCount(typeof<SignalDocumentActivity>.Name))

        let signals = context.GetCalls<DocumentSignal>(typeof<SignalDocumentActivity>.Name)
        Assert.True(signals |> List.exists (fun s -> s.EventName = "SettlementFailed"))
        Assert.False(signals |> List.exists (fun s -> s.EventName = "SettlementCompleted"))
    }
