# Workflow data references

The `WorkflowDataReferences` console example demonstrates opt-in payload offloading
and data references using **Dapr.Workflow**.
There is no separate `Dapr.Workflow.BinaryStore` package and no public converter to
implement.

The default run walks through **inline**, **automatic**, and **explicit-reference**
approaches in that order, with a labeled console section for each. Every approach
starts with the same document containing 128 KiB of text. This is a behavior
comparison, not a performance benchmark: the first two uppercase the document,
while the third streams its serialized bytes and returns a small summary.

## Follow the code

Each approach has its own class containing its service registration and client code:

| Approach | Start here | What is in history? | Who reads the document? |
|---|---|---|---|
| Inline | [InlinePayloadExample](./Examples/InlinePayloadExample.cs) | Full serialized documents in workflow/activity inputs and outputs. | The SDK deserializes ordinary inputs for the workflow and activity. No binary-store calls. |
| Automatic | [AutomaticPayloadExample](./Examples/AutomaticPayloadExample.cs) | Reference envelopes for payloads above the 64 KiB threshold. Small values stay inline. | The SDK retrieves and deserializes referenced values for ordinary inputs and client output reads. |
| Explicit references | [ReferencePayloadExample](./Examples/ReferencePayloadExample.cs) | A typed reference as input; a small inline summary as output. | The workflow only forwards the reference; the activity streams bytes without deserializing the document. Client completion inspection also resolves the input. |

Inline and automatic deliberately share
[DocumentProcessingWorkflow](./Workflows/DocumentProcessingWorkflow.cs) and
[UppercaseDocumentActivity](./Activities/UppercaseDocumentActivity.cs). Only
registration changes: automatic chains `WithBinaryStorePayloads(...)` from
`services.AddDaprWorkflow(...)`. Each class explicitly registers its workflows and
activities. The SDK handles offloading and rehydration without
changing their `DocumentPayload` parameters or return types.

The explicit-reference example uses
[StreamingPayloadWorkflow](./Workflows/StreamingPayloadWorkflow.cs) and
[MeasurePayloadActivity](./Activities/MeasurePayloadActivity.cs), whose inputs are
`PayloadReference<DocumentPayload>`. The activity reads through a 16 KiB buffer and
checks both the byte count and SHA-256 against the reference. Storage I/O stays in
the client or activity, not in the replayed orchestrator.

The examples request inputs and outputs when waiting for completion so they can
display and check results. This also resolves referenced input on the client,
including in the explicit-reference case. Passing `getInputsAndOutputs: false`
avoids that inspection read when only completion status is needed; it also omits
the summary output. This demo does not claim a single download end to end.

[Program.cs](./Program.cs) only selects the examples and manages host lifetime.
[PayloadExample](./Examples/PayloadExample.cs) shares completion/result checks.
Offloading is configured per host, so each approach gets a fresh host and the
previous worker is stopped before the next starts. All three use one dedicated
sidecar sequentially.

## Prerequisites

- .NET 8, 9, 10 or 11 SDK. The commands below select .NET 10; replace `net10.0` with
  `net8.0`, `net9.0` ir `net10.0` if needed.
- Dapr CLI and a local Dapr runtime supporting binary stores, **1.19 or later**
  (also supported by `1.19.0-rc.3`).
- An initialized local Dapr environment with Redis, placement, and scheduler
  running. `components/statestore.yaml` assumes the default local Redis endpoint,
  `localhost:6379`; adjust it if your setup differs.

Check the installed runtime with `dapr --version`. The workflow protocol itself
does not need runtime-native payload-reference support; the SDK uses the binary
store API and persists version-1 reference envelopes.

## Run

From this directory, start a dedicated sidecar in one terminal and leave it running:

```powershell
dapr run --app-id workflow-data-references --dapr-http-port 3500 --dapr-grpc-port 50001 --resources-path .\components
```

In a second terminal, from the same directory:

```powershell
dotnet run --framework net10.0
```

This runs all three approaches. `--DataRefs:Enabled=true` has the same effect.
Do not attach another worker to this app ID while running the comparison; an
inline-only worker cannot resolve an offloaded workflow's references.

The app connects to the default gRPC endpoint on port 50001. If using another port,
set `DAPR_GRPC_ENDPOINT` to the same endpoint for the workflow and binary-store
clients.

Alongside SDK logs, expect output resembling:

```text
Workflow payload approaches
Each approach starts with the same document: sample.txt, 131,072 characters.
Inline and automatic uppercase the document; explicit references stream and measure its serialized bytes.
This is a behavior comparison, not a performance benchmark.

=== 1/3: Inline payloads (no binary store) ===
Registration: AddDaprWorkflow(...) only; payload storage is not enabled.
History: full serialized documents in workflow and activity inputs/outputs.
...
Workflow ID: payload-inline-...
Result: 131072 characters, first character 'A'.
Takeaway: the document itself travels through workflow history.

=== 2/3: Automatic offloading (same application code) ===
Registration: AddDaprWorkflow(...).WithBinaryStorePayloads(...), threshold 64 KiB.
...
Workflow ID: payload-auto-...
Result: 131072 characters, first character 'A'.
Persisted output reference: {"$daprRef":{"v":1,"store":"workflow-payloads",...}}
Takeaway: smaller history does not mean zero document reads; the SDK still materializes ordinary inputs.

=== 3/3: Explicit references (workflow forwards, activity streams) ===
...
Workflow ID: payload-ref-...
Client stored ... serialized bytes; input reference: {"$daprRef":{...}}
Result: activity streamed ... serialized bytes using a 16 KiB buffer.
SHA-256 verified: ...
Serialized bytes include JSON structure, not just the document's characters.
Client inspection: fetching inputs/outputs at completion also resolves the referenced input on the client.
Takeaway: the workflow never reads the document; the activity streams bytes instead of deserializing it.

Histories are retained for inspection using the printed workflow IDs.
Keep the demo sidecar running while referenced payloads are needed; its binary store is in-memory.
```

The stored byte count includes JSON property names and the document name, so it is
larger than the content's character count. Workflow failures or unexpected results
cause the app to fail rather than print a success message.

To compare the persisted representations, substitute each printed workflow ID:

```powershell
curl.exe http://localhost:3500/v1.0/workflows/dapr/payload-inline-REPLACE_WITH_ID
curl.exe http://localhost:3500/v1.0/workflows/dapr/payload-auto-REPLACE_WITH_ID
curl.exe http://localhost:3500/v1.0/workflows/dapr/payload-ref-REPLACE_WITH_ID
```

Inline input/output contains the full document. Automatic input/output contains
reference envelopes, while SDK `ReadOutputAs<DocumentPayload>()` returns the
document and `ReadOutputAs<PayloadReference<DocumentPayload>>()` exposes the retained
wire reference. Explicit-reference input contains an envelope, but output is only
the byte-count/hash summary.

### Compare with payload support disabled

With the sidecar still running, execute:

```powershell
dotnet run --framework net10.0 -- --DataRefs:Enabled=false
```

Only `InlinePayloadExample` runs, labeled `1/1`. The app does not call
`WithBinaryStorePayloads`: it still processes all 131072 characters, but raw
input/output stays inline and no workflow binary-store services are activated.
Merely having the component available does not enable this capability.

Every run creates fresh workflows. Before rerunning, let any workflows from a
previous interrupted run finish with their original configuration. Do **not**
disable readers for existing offloaded histories. To stop new automatic writes while continuing to read old
references, keep `WithBinaryStorePayloads` and set
`options.OffloadSites = PayloadKind.None` instead.

## Retention and production use

The included **in-memory binary store is only for a disposable demo**. Its payloads
are lost when the sidecar stops, even though workflow histories remain in Redis.
Do not expect those histories to replay or their payloads to resolve afterward.
Use a durable binary-store component in production, and retain immutable payloads
for as long as any workflow history, result, child, or rerun references them.

This example intentionally does not configure a durable `IWorkflowPayloadManifest`
or delete payloads. Workflow completion and history purging do not automatically
clean up payload storage. For production cleanup, register a durable manifest with
`WithPayloadManifest<TManifest>()` and invoke `WorkflowPayloadCleaner` only after
all consumers have released their references.

The project references SDK projects to use the implementation in this repository.
A packaged application uses the `Dapr.Workflow` NuGet package; it does not need to
reference a workflow binary-store implementation package.
