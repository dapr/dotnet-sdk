# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using the console.

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)


## Optional Setup
Dapr workflow, as well as this example program, now support authentication through the use of API tokens. For more information on this, view the following document: [API Token](https://github.com/dapr/dotnet-sdk/blob/master/docs/api-tokens.md)

### Execution and recovery

`WithSerializer` and `WithJsonSerializer` configure both the worker and the
DI-registered public client, with or without binary-store payloads. Explicit
client-builder serializer overrides still take precedence.

Suspension defers history delivery until resume; termination remains terminal
even when other events follow it in a batch. `ContinueAsNew` preserves late,
unprocessed external events by default, including events for abandoned waits.
Return immediately after calling it.

History-fetch and completion-delivery failures retire the affected worker stream
so the sidecar can redeliver its work. Activities must remain idempotent: a failed
acknowledgment can cause a successful activity to run again. Queued work from a
retired stream is canceled, but already-running user activities cannot be forcibly
stopped. Completion waits return state and requested payloads from one runtime
snapshot rather than fetching the instance again.

### Generated registration and version routing

`AddDaprWorkflowVersioning()` combines generated workflow contributions from all
participating assemblies before choosing each family's latest version. The
configured selector, default strategy, and family strategy/named-options overrides
apply to the combined family. Explicit `RegisterWorkflow` registrations take
precedence over generated aliases. Register contributions before the workflow
factory is created; loading additional workflow assemblies after worker startup
is not a hot-reload mechanism.

Resolved latest names are fully qualified (for example, `global::MyApp.OrderV2`),
so types with the same simple name in different namespaces remain distinguishable.
Canonical aliases, existing simple-name aliases, and explicit aliases remain valid.
Keep the concrete names used by existing histories registered for replay. Colliding
simple-name aliases cannot identify both types; use fully qualified names instead.
Legacy generated registration delegates are still supported and their routes are
combined, but rebuild those assemblies to participate in global version selection.

Discovery skips abstract/open-generic types, including types nested in generic
containers, and types inaccessible from the generated file (including file-local
types). This applies to workflows and activities, including reference scanning.
The dependency-injection analyzer offers parameter removal only when it can avoid
changing parameter usages, constructor calls, and overload sets. Move required
service-dependent work into activities rather than deleting initialization logic.
Type-safety diagnostics check serialized contracts: an implicit conversion operator
does not run merely because a workflow input is passed as `object`. Convert the
value explicitly before sending it, or use matching input/output contracts.

### Optional binary-store payloads

For a runnable comparison of inline payloads, automatic offloading, and explicit
reference streaming, see [WorkflowDataReferences](./WorkflowDataReferences).

SDK-managed payload storage is included in `Dapr.Workflow`; a separate
`Dapr.Workflow.BinaryStore` package is not required. It uses the runtime's existing
binary store API, without changing the workflow protocol or requiring runtime
support for workflow references. Without this opt-in, workflow serialization is
unchanged.

```csharp
using Dapr.Workflow;
using Dapr.Workflow.Payloads;

builder.Services.AddDaprWorkflow()
    .WithBinaryStorePayloads(options =>
    {
        options.StoreName = "payload-store";
        options.WritePreference = PayloadWritePreference.PreferReference;
    });
```

`WithBinaryStorePayloads` extends the workflow builder returned by `AddDaprWorkflow()`
or `AddDaprWorkflowBuilder(...)`, not `IServiceCollection`. For subsequent service
registrations, use `builder.Services` or the workflow builder's `Services` property.

Registration alone defaults to `PayloadWritePreference.Preserve`: ordinary values
stay inline and explicit references remain references. The `PreferReference`
preference enables automatic offloading after the configured `IDaprSerializer`
runs. With the default threshold, payloads of at least 64 KiB are offloaded at workflow,
activity, child-workflow, event, continue-as-new, and custom-status boundaries.
`WritePreference`, `OffloadSites`, and `OffloadThreshold`
control new automatic writes. Detached workflow inputs,
rerun input overrides, and termination outputs use the corresponding workflow
input/output policy. Register this support on every participating client and worker,
including cross-application destinations, with compatible serializers and access
to the same stored data.

The codec, boundary coordination, and feature registration/access hooks are internal;
they do not add a generic feature API to `WorkflowContext`. The worker resolves inputs before
invoking application code, drains asynchronous result reads between history events,
and offloads pending actions after replay has removed already-recorded actions.
Stored bytes are the original serialized snapshot, not a second serialization of it.
The version-1 reference envelope remains unchanged; raw and propagated history APIs
continue to expose these stored envelopes rather than rewriting history.

Clients and activities can also inject `IWorkflowPayloadResolver`, call `StoreAsync`
to obtain a `PayloadReference<T>`, and pass that reference through the workflow.
Explicit reference inputs bypass automatic materialization on read. Outgoing
references remain subject to the write preference and cross-app rules. Activities
can use `ResolveAsync<T>` or `OpenAsync`; orchestrators should use
`context.ResolvePayloadAsync(reference)` or `context.OpenPayloadAsync(reference)`.
For streams inside orchestrators, use asynchronous reads and `await using`.
Do not inject the resolver directly into orchestrators or upload payloads from them.

Asynchronous state retrieval prepares payloads before returning, so existing
`ReadInputAs<T>`, `ReadOutputAs<T>`, and `ReadCustomStatusAs<T>` remain synchronous
without blocking on storage. An oversized payload remains readable as
`PayloadReference<T>`; requesting its materialized value throws the configured
rehydration-limit error. Missing payloads and other read failures are surfaced.

To stop automatic offloading while retaining compatibility with existing histories,
keep registration and storage access but set `WritePreference = PayloadWritePreference.Preserve`.
Ordinary payloads are written inline regardless of the configured sites and threshold;
`OffloadSites = PayloadKind.None` remains an alternative. The switch is host-wide:
it affects future writes for new and running workflows, not existing history.
Explicit references still pass through, and `IWorkflowPayloadResolver.StoreAsync`
still stores payloads. If migrating explicit `PayloadReference<T>` contracts to
ordinary `T` values, use a new workflow version for new instances and keep the old
workflow/activity contracts registered for existing instances. See the
[migration guidance](./WorkflowDataReferences/README.md#switch-back-to-inline-payloads).
This does not migrate or delete existing data or insert workflow-versioning patches.

`PreferInline` additionally resolves outgoing top-level references to their original
serialized content. Failures are surfaced, not replaced with reference fallbacks.
Use it only when receivers accept ordinary values; reference-typed contracts need
versioned migration. `Preserve` does not track provenance: a reference read as `T`
and later returned as `T` is a new inline value.

Cross-app activity, child, and detached workflow inputs require inline data by
default, overriding automatic offloading. Set `AllowPayloadReferences: true` in
the call's `WorkflowTaskOptions` or `ChildWorkflowTaskOptions` only when the destination
supports references and can access the stored objects. This permits the configured
write preference; it does not override `PreferInline`. It applies only to top-level
inputs, not nested references, results, or propagated history. Inlining checks
`MaxInMemoryRehydrationSize` and configured integrity checks. Transport/runtime limits
still apply, including message overhead; a successful buffering check does not guarantee
delivery. Inbound ordinary contracts accept inline data or resolvable references without
rewriting history. Only the .NET SDK currently supports the reference format.

Payload cleanup is explicit. Register a durable `IWorkflowPayloadManifest` with
`WithPayloadManifest<TManifest>()` and use `WorkflowPayloadCleaner` only when no
retained history, result, or other workflow needs the references. Purging a workflow
does not currently invoke this cleaner automatically. Without a durable manifest,
cleanup is a no-op and payload retention must be managed separately.

## Projects in sample

The main sample is [WorkflowConsoleApp](./WorkflowConsoleApp).
It utilizes the workflow SDK as well as the workflow management API for simulating inventory management and sale of goods in a store.
The main `Program.cs` file contains the main setup of the app, the registration of the workflow and its activities, and interaction with the user. The workflow definition is found in the `Workflows` directory and the workflow activity definitions are found in the `Activities` directory.

There are five activities in the directory that could be called by the workflows:
- `NotifyActivity`:  printing logs as notifications
- `ProcessPaymentActivity`: printing logs and delaying for simulating payment processing
- `RequestApprovalActivity`: printing logs to indicate that the order has been approved
- `ReserveInventoryActivity`: checking if there are enough items for purchase
- `UpdateInventoryActivity`: updating the statestore according to purchasing

The `OrderProcessingWorkflow.cs` in `Workflows` directory implements the running logic of the workflow. Based on the purchase stage and outcome, it calls different activities and waits for the corresponding events to trigger interaction with the user.

The separate [WorkflowDataReferences](./WorkflowDataReferences) console project demonstrates
SDK-managed binary-store payloads and includes its own local component definitions
and run instructions.

This sample also contains a [WorkflowUnitTest](./WorkflowUnitTest) .NET project that utilizes [xUnit](https://xunit.net/) and [Moq](https://github.com/moq/moq) to test the workflow logic.
It works by creating an instance of the `OrderProcessingWorkflow` (defined in the `WorkflowConsoleApp` project), mocking activity calls, and testing the inputs and outputs.
The tests verify the outputs of the workflow without requiring a Dapr sidecar, since the `WorkflowContext` itself is mocked.

### Test scenarios

| Test | Concept |
|------|---------|
| `TestSuccessfulOrder` | Mock activity calls, verify inputs and call counts |
| `TestHighCostOrderApproved` | Mock external events (`WaitForExternalEventAsync`) and verify custom status |
| `TestHighCostOrderApprovalTimeout` | Simulate timeouts with `TaskCanceledException` |
| `TestInsufficientInventory` | Branch on activity return value, verify early termination |
| `TestActivityException` | Simulate activity failures with `WorkflowTaskFailedException` |

### Running unit tests

No Dapr sidecar is required. From the repository root:

```sh
dotnet test examples/Workflow/WorkflowUnitTest
```

## Running the console app example

To run the workflow web app locally, two separate terminal windows are required.
In the first terminal window, start the dapr sidecar:

```sh
dapr run --app-id wfapp --dapr-grpc-port 50001 --dapr-http-port 3500
```

Next, in a separate terminal window, from the `WorkflowConsoleApp` directory, run the following command to start the program itself:

```sh
dotnet run
```

Dapr listens for HTTP requests at `http://localhost:3500`.

This example illustrates a purchase order processing workflow. The console prompts provide directions on how to both purchase and restock items.

To start a workflow, you have two options:

1. Follow the directions from the console prompts.
2. Use the workflows API and send a request to Dapr directly. Examples are included below as well as in the "demo.http" file down the "WorkflowConsoleApp" directory.

For the workflow API option, two identical `curl` commands are shown, one for Linux/macOS (bash) and the other for Windows (PowerShell). The body of the request is the purchase order information used as the input of the workflow. 

Make note of the "1234" in the commands below. This represents the unique identifier for the workflow run and can be replaced with any identifier of your choosing.

```bash
curl -i -X POST http://localhost:3500/v1.0-beta1/workflows/dapr/OrderProcessingWorkflow/start?instanceID=1234 \
  -H "Content-Type: application/json" \
  -d '{"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}'
```

On Windows (PowerShell):

```powershell
curl -i -X POST http://localhost:3500/v1.0-beta1/workflows/dapr/OrderProcessingWorkflow/start?instanceID=1234 `
  -H "Content-Type: application/json" `
  -d '{"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}'
```

If successful, you should see a response like the following: 

```json
{"instanceID":"1234"}
```

Next, send an HTTP request to get the status of the workflow that was started:

```bash
curl -i -X GET http://localhost:3500/v1.0-beta1/workflows/dapr/1234
```

The workflow is designed to take several seconds to complete. If the workflow hasn't completed yet when you issue the previous command, you should see the following JSON response (formatted for readability):

```json
{
  "instanceID": "1234",
  "workflowName": "OrderProcessingWorkflow",
  "createdAt": "2023-05-10T00:42:03.911444105Z",
  "lastUpdatedAt": "2023-05-10T00:42:06.142214153Z",
  "runtimeStatus": "RUNNING",
  "properties": {
    "dapr.workflow.custom_status": "",
    "dapr.workflow.input": "{\"Name\": \"Paperclips\", \"TotalCost\": 99.95, \"Quantity\": 1}"
  }
}
```

Once the workflow has completed running, you should see the following output, indicating that it has reached the "COMPLETED" status:

```json
{
  "instanceID": "1234",
  "workflowName": "OrderProcessingWorkflow",
  "createdAt": "2023-05-10T00:42:03.911444105Z",
  "lastUpdatedAt": "2023-05-10T00:42:18.527704176Z",
  "runtimeStatus": "COMPLETED",
  "properties": {
    "dapr.workflow.custom_status": "",
    "dapr.workflow.input": "{\"Name\": \"Paperclips\", \"TotalCost\": 99.95, \"Quantity\": 1}",
    "dapr.workflow.output": "{\"Processed\":true}"
  }
}
```

When the workflow has completed, the stdout of the workflow app should look like the following:

```log
info: WorkflowConsoleApp.Activities.NotifyActivity[0]
      Received order 1234 for Paperclips at $99.95
info: WorkflowConsoleApp.Activities.ReserveInventoryActivity[0]
      Reserving inventory: 1234, Paperclips, 1
info: WorkflowConsoleApp.Activities.ProcessPaymentActivity[0]
      Processing payment: 1234, 99.95, USD
info: WorkflowConsoleApp.Activities.NotifyActivity[0]
      Order 1234 processed successfully!
```

If you have Zipkin configured for Dapr locally on your machine, then you can view the workflow trace spans in the Zipkin web UI (typically at http://localhost:9411/zipkin/).