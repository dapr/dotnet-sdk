# Approvals + workflows

Document types are defined as state-machine configuration and onboarded at runtime; a single compiled interpreted actor runs every one, and an approved document hands off to a settlement workflow that retries and compensates.

Tutorial: [Part 6 - Composing interpreted actors with workflows](../../../docs/dotnet-actorsnext/tutorial/part-6.md).

Each document (an expense report, a contract) is a long-lived, addressable entity, so it belongs in a state-machine actor. Its behavior is an `InterpretedMachineDefinition` document, verified by `InterpretedMachineVerifier` and stored by `InterpretedMachineDeployer` before it goes live, then executed by the single compiled `InterpretedStateMachineActor`. Named guards and effects such as `WithinApprovalLimit` and `StartSettlement` resolve through an `ICapabilityRegistry` of vetted, compiled actions.

Settlement is a finite, failure-prone process, so it belongs in a workflow, not the actor. When a document enters `Approved`, the `StartSettlement` effect schedules `SettlementWorkflow` with a **deterministic** instance id derived from the actor type and id, so an at-least-once re-run of the approving turn re-schedules the same workflow instead of starting a second one. The workflow fans out notifications, charges under a retry policy, and — if the charge exhausts its retries — runs a compensation activity and drives the document back to `SettlementFailed` through `IDynamicActorClient`. On success it drives the document to `Archived`. The two compose in both directions: the actor starts the workflow, and the workflow drives the actor.

There are two practical layers that comprise this approach: 
  1) The compiled layer ships in the assembly and is registered once at startup: the single `InterpretedStateMachineActor` type, the `ICapabilityRegistry` of vetted guards and effects (`WithinApprovalLimit`, `StartSettlement`, and the rest), the verifier, deployer, and store, and the settlement workflow. None of it knows about a specific document type. 
  2) The dynamic layer is the `InterpretedMachineDefinition` itself — states, transitions, branches, and the guard and effect names it composes from — authored as data and deployed per document at onboarding time. The C# `ExpenseReport()` and `Contract()` builders may look like logic, but they are data factories: their output is the dynamic part, and in a real system that document could just as easily arrive as JSON from a database or an HTTP body with no recompile.

The two layers meet by name. Just like invoking an actor requires its name, a definition never holds a compiled action; it holds a string like `"StartSettlement"` that binds to the compiled effect at runtime through the capability registry. Verification is the single checkpoint that guarantees that binding will succeed: `InterpretedMachineVerifier` runs inside `InterpretedMachineDeployer.DeployAsync` — right before the definition is stored — and rejects any definition whose shape is unsound (a dead-end or unreachable state) or that references a guard or effect the registry does not vet. A bad document type is therefore caught at onboarding, not on a live document.

Because the behavior is data and the runtime is compiled, adding a new document type is configuration rather than a code deploy, and the one compiled actor never has to be re-registered.

## Prerequisites

Run `dapr init` once so a default state store with `actorStateStore` enabled is available. Both the actor runtime and the workflow engine use it. Dapr 1.18 requires the sidecar to have a gRPC app channel; the sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP control-plane API, and port `5056` is the HTTP/2 app channel used by daprd.

## Start the Dapr runtime

```powershell
dapr run --app-id actors-example-06 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5056 -- dotnet run
```

## Run Locally

Or start the app separately from this sample directory (it assumes the gRPC port `50001`):

```powershell
cd examples\Actor.Next\06-Approvals
dotnet run
```

Then open `Approvals.Next.Example06.http` in Rider or Visual Studio and run the requests in order. The happy path (`exp-001`) auto-approves a small expense and the settlement workflow drives it to `Archived`. The `exp-002` path escalates a large expense to a manager and then fails the charge, so the workflow compensates and drives the document to `SettlementFailed`. The `con-001` path shows a different document type (a contract, with a legal-review stage) running on the same compiled actor.

The workflow logs each step (notify, charge, compensate, signal), so watch the app's console output to follow settlement.

## Run Tests

```powershell
dotnet test Approvals.Next.Example06.Tests\Approvals.Next.Example06.Tests.csproj --no-restore
```

The tests use `ActorTestRuntime` and a mocked `WorkflowContext`; they need no sidecar, no state store, and no Docker. They drive a document through the interpreted machine, prove a stranded or unregistered-effect definition is rejected before rollout, assert the approving turn schedules the settlement workflow with a deterministic id (and does not double-schedule on a re-run), and exercise both the success and compensation paths of the settlement workflow.
