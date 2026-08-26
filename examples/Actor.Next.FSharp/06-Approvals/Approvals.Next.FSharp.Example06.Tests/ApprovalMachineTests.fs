namespace Approvals.Next.FSharp.Example06.Tests

open System
open System.Reflection
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.Extensions.DependencyInjection
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted
open Dapr.Actors.Next.Testing
open Dapr.Actors.Next.Examples.Approvals
open Dapr.Workflow
open Xunit

type ApprovalMachineTests() =

    static member private Expense = ActorId.Create("exp-1")

    static member private Drive(runtime: ActorTestRuntime, invoke: Task<string>) : Task<string> =
        task {
            do! runtime.RunToIdle()
            let! json = invoke
            if isNull json then
                return null
            else
                use document = JsonDocument.Parse(json)
                return document.RootElement.GetProperty("State").GetString()
        }

    static member private CreateRuntime(store: InMemoryInterpretedMachineStore, workflowClient: IDaprWorkflowClient) : ActorTestRuntime =
        new ActorTestRuntime(Action<IServiceCollection>(fun services ->
            services.AddSingleton<IActorRegistry>(ApprovalTypeRegistry() :> IActorRegistry) |> ignore
            services.AddSingleton<IInterpretedMachineStore>(store) |> ignore
            services.AddSingleton(workflowClient) |> ignore
            services.AddSingleton<ICapabilityRegistry>(fun _ -> ApprovalCapabilityRegistry(workflowClient) :> ICapabilityRegistry) |> ignore
            services.AddDaprInterpretedActors(ApprovalDefinitions.ActorType) |> ignore
        ))

    static member private Deploy(store: InMemoryInterpretedMachineStore, workflowClient: IDaprWorkflowClient, definition: InterpretedMachineDefinition) : Task =
        task {
            let capabilities = ApprovalCapabilityRegistry(workflowClient) :> ICapabilityRegistry
            let verifier = InterpretedMachineVerifier(capabilities)
            let deployer = InterpretedMachineDeployer(verifier :> IInterpretedMachineVerifier, store :> IInterpretedMachineStore)
            do! deployer.DeployAsync(ApprovalDefinitions.ActorType, ApprovalMachineTests.Expense, definition)
        }

    static member private Provider(runtime: ActorTestRuntime) : IServiceProvider =
        let field = runtime.GetType().GetField("provider", BindingFlags.Instance ||| BindingFlags.NonPublic)
        field.GetValue(runtime) :?> IServiceProvider

    [<Fact>]
    member this.Small_expense_auto_approves_and_starts_settlement_workflow() = task {
        let workflowClient = new RecordingWorkflowClient()

        let store = InMemoryInterpretedMachineStore()
        do! ApprovalMachineTests.Deploy(store, workflowClient :> IDaprWorkflowClient, ApprovalDefinitions.ExpenseReport())
        use runtime = ApprovalMachineTests.CreateRuntime(store, workflowClient :> IDaprWorkflowClient)
        let controlPlane = ApprovalControlPlane(
            ApprovalMachineTests.Provider(runtime).GetRequiredService<IActorRegistry>(),
            ApprovalMachineTests.Provider(runtime).GetRequiredService<IDynamicActorClient>())

        let submission : SubmitDocument = {
            Requester = "alice"
            Amount = 250m
            Parties = ([| "finance"; "alice" |] :> IReadOnlyList<string>)
            SimulateChargeFailure = false
        }
        let! state1 = ApprovalMachineTests.Drive(runtime, controlPlane.SubmitAsync(ApprovalMachineTests.Expense.Value, submission, CancellationToken.None))
        Assert.Equal("Submitted", state1)

        let! state2 = ApprovalMachineTests.Drive(runtime, controlPlane.BeginReviewAsync(ApprovalMachineTests.Expense.Value, CancellationToken.None))
        Assert.Equal("InReview", state2)

        let! state3 = ApprovalMachineTests.Drive(runtime, controlPlane.ApproveAsync(ApprovalMachineTests.Expense.Value, { Approver = "bob"; Note = null }, CancellationToken.None))
        Assert.Equal("Approved", state3)

        let expected = StartSettlementEffect.InstanceIdFor(ApprovalDefinitions.ActorType, ApprovalMachineTests.Expense.Value)
        Assert.True(workflowClient.Scheduled.Count = 1)
        Assert.Equal(expected, workflowClient.Scheduled.[0])
    }

    [<Fact>]
    member this.Large_expense_escalates_to_a_manager_before_approval() = task {
        let workflowClient = new RecordingWorkflowClient()

        let store = InMemoryInterpretedMachineStore()
        do! ApprovalMachineTests.Deploy(store, workflowClient :> IDaprWorkflowClient, ApprovalDefinitions.ExpenseReport())
        use runtime = ApprovalMachineTests.CreateRuntime(store, workflowClient :> IDaprWorkflowClient)
        let controlPlane = ApprovalControlPlane(
            ApprovalMachineTests.Provider(runtime).GetRequiredService<IActorRegistry>(),
            ApprovalMachineTests.Provider(runtime).GetRequiredService<IDynamicActorClient>())

        let submission : SubmitDocument = {
            Requester = "carol"
            Amount = 8500m
            Parties = ([| "finance" |] :> IReadOnlyList<string>)
            SimulateChargeFailure = false
        }
        let! _ = ApprovalMachineTests.Drive(runtime, controlPlane.SubmitAsync(ApprovalMachineTests.Expense.Value, submission, CancellationToken.None))
        let! _ = ApprovalMachineTests.Drive(runtime, controlPlane.BeginReviewAsync(ApprovalMachineTests.Expense.Value, CancellationToken.None))

        let! state1 = ApprovalMachineTests.Drive(runtime, controlPlane.ApproveAsync(ApprovalMachineTests.Expense.Value, { Approver = "bob"; Note = null }, CancellationToken.None))
        Assert.Equal("Escalated", state1)
        Assert.True(workflowClient.Scheduled.Count = 0)

        let! state2 = ApprovalMachineTests.Drive(runtime, controlPlane.ApproveAsync(ApprovalMachineTests.Expense.Value, { Approver = "manager"; Note = null }, CancellationToken.None))
        Assert.Equal("Approved", state2)
        Assert.True(workflowClient.Scheduled.Count = 1)
    }
