# Workflow data references

The `WorkflowDataReferences` console example demonstrates opt-in payload offloading
and data references using **Dapr.Workflow**.
There is no separate `Dapr.Workflow.BinaryStore` package and no public converter to
implement.

The default run walks through **inline**, **automatic offloading**, **explicit references**,
**automatic references**, and **explicit chunks in workflows and activities** in that order,
with a labeled console section for each. Every approach
starts with the same document containing 128 KiB of text. This is a behavior
comparison, not a performance benchmark: the first two uppercase the document,
while the last three stream its serialized bytes and return a small summary.

## Follow the code

Each approach has its own class containing its service registration and client code:

| Approach | Start here | What is in history? | Who reads the document? |
|---|---|---|---|
| Inline | [InlinePayloadExample](./Examples/InlinePayloadExample.cs) | Full serialized documents in workflow/activity inputs and outputs. | The SDK deserializes ordinary inputs for the workflow and activity. No binary-store calls. |
| Automatic | [AutomaticPayloadExample](./Examples/AutomaticPayloadExample.cs) | Reference envelopes for payloads above the 64 KiB threshold. Small values stay inline. | The SDK retrieves and deserializes referenced values for ordinary inputs and client output reads. |
| Explicit references | [ReferencePayloadExample](./Examples/ReferencePayloadExample.cs) | A typed reference as input; a small inline summary as output. | The workflow only forwards the reference; the activity streams bytes without deserializing the document. Client completion inspection also resolves the input. |
| Automatic references | [AutomaticReferencePayloadExample](./Examples/AutomaticReferencePayloadExample.cs) | The SDK converts an ordinary document into an input reference, regardless of size; the summary stays inline. | The same reference-typed workflow and activity as the explicit variation; only the sender's registration and scheduling code differ. |
| Explicit chunks | [ChunkStreamingPayloadExample](./Examples/ChunkStreamingPayloadExample.cs) | Explicit references as workflow/activity inputs; a small inline summary as output. No automatic offloading. | The workflow explicitly enumerates chunks and checks their total size; the activity independently enumerates chunks and verifies size and SHA-256. Replay and client inspection can cause additional reads. |

Inline and automatic deliberately share
[DocumentProcessingWorkflow](./Workflows/DocumentProcessingWorkflow.cs) and
[UppercaseDocumentActivity](./Activities/UppercaseDocumentActivity.cs). Only
registration changes: automatic selects `WritePreference = PayloadWritePreference.PreferReference`
in `WithBinaryStorePayloads(...)`, chained from
`services.AddDaprWorkflow(...)`. Each class explicitly registers its workflows and
activities. The SDK handles offloading and rehydration without
changing their `DocumentPayload` parameters or return types.

The explicit-reference and automatic-reference examples use
[StreamingPayloadWorkflow](./Workflows/StreamingPayloadWorkflow.cs) and
[MeasurePayloadActivity](./Activities/MeasurePayloadActivity.cs), whose inputs are
`PayloadReference<DocumentPayload>`. The activity reads with `await foreach` over `resolver.ReadChunksAsync(...)` and
checks both the byte count and SHA-256 against the reference. Storage I/O stays in
the client or activity, not in the replayed orchestrator.

The explicit-chunks example instead uses
[ChunkStreamingWorkflow](./Workflows/ChunkStreamingWorkflow.cs), which reads through
`context.ReadPayloadChunksAsync(...)` before calling the same `MeasurePayloadActivity`.
It keeps `WritePreference = Preserve` and `OffloadSites = None`: the client explicitly
creates a reference, and both consumers explicitly enumerate stored bytes.

The examples request inputs and outputs when waiting for completion so they can
display and check results. This also resolves referenced input on the client,
including in all reference-input cases. Passing `getInputsAndOutputs: false`
avoids that inspection read when only completion status is needed; it also omits
the summary output. This demo does not claim a single download end to end.

[Program.cs](./Program.cs) only selects the examples and manages host lifetime.
[PayloadExample](./Examples/PayloadExample.cs) shares completion/result checks.
Offloading is configured per host, so each approach gets a fresh host and the
previous worker is stopped before the next starts. All five use one dedicated
sidecar sequentially.

### Two ways to create the same input reference

`ReferencePayloadExample` keeps the default `WritePreference = Preserve` and explicitly
uploads the document before scheduling:

```csharp
var reference = await resolver.StoreAsync(document, instanceId, cancellationToken);
await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, reference,
    startTime: null, cancellation: cancellationToken);
```

`AutomaticReferencePayloadExample` instead configures the sending host to always
offload workflow inputs:

```csharp
services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<StreamingPayloadWorkflow>();
    options.RegisterActivity<MeasurePayloadActivity>();
}).WithBinaryStorePayloads(options =>
{
    options.StoreName = "workflow-payloads";
    options.WritePreference = PayloadWritePreference.PreferReference;
    options.OffloadSites = PayloadKind.WorkflowInput;
    options.OffloadThreshold = null;
});

await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, document,
    startTime: null, cancellation: cancellationToken);
```

Here the caller passes `DocumentPayload`, but the workflow receives
`PayloadReference<DocumentPayload>`. The SDK uploads the document before submitting
the workflow input. The workflow then forwards that reference to the activity;
outputs remain inline because only `WorkflowInput` is enabled for automatic offloading.

**The receiving type does not trigger reference creation.** `OffloadThreshold = null`
ensures even small documents become references. A numeric threshold would leave
smaller inputs inline, which a `PayloadReference<T>` contract rejects. The explicit
`StoreAsync` variation does not depend on the threshold or automatic-offloading sites.
With `ReferencePayloadExample`'s default `Preserve` policy, passing the document directly would fail
the reference-typed contract. Both clients and workers need payload support registered.

The same sender-side conversion supports ordinary values passed to reference-typed
activities when `ActivityInput` is enabled. Cross-app calls additionally require
`AllowPayloadReferences = true` and destination access to the stored payload.
This sample demonstrates conversion at the workflow input; the activity receives
an already-created reference.

Neither upload variation is an end-to-end bounded-memory stream: both currently
buffer serialized data before uploading. Streaming here describes how the activity
reads the stored bytes.

## Prerequisites

- .NET 10 SDK.
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

This runs all five approaches. `--DataRefs:Enabled=true` has the same effect.
Do not attach another worker to this app ID while running the comparison; an
inline-only worker cannot resolve an offloaded workflow's references.

The app connects to the default gRPC endpoint on port 50001. If using another port,
set `DAPR_GRPC_ENDPOINT` to the same endpoint for the workflow and binary-store
clients.

Alongside SDK logs, expect output resembling:

```text
Workflow payload approaches
Each approach starts with the same document: sample.txt, 131,072 characters.
Inline and automatic offloading uppercase the document; the reference-input variations stream and measure its serialized bytes.
This is a behavior comparison, not a performance benchmark.

=== 1/5: Inline payloads (no binary store) ===
Registration: AddDaprWorkflow(...) only; payload storage is not enabled.
History: full serialized documents in workflow and activity inputs/outputs.
...
Workflow ID: payload-inline-...
Result: 131072 characters, first character 'A'.
Takeaway: the document itself travels through workflow history.

=== 2/5: Automatic offloading (same application code) ===
Registration: AddDaprWorkflow(...).WithBinaryStorePayloads(...), threshold 64 KiB.
...
Workflow ID: payload-auto-...
Result: 131072 characters, first character 'A'.
Persisted output reference: {"$daprRef":{"v":1,"store":"workflow-payloads",...}}
Takeaway: smaller history does not mean zero document reads; the SDK still materializes ordinary inputs.

=== 3/5: Explicit references (workflow forwards, activity streams) ===
...
Workflow ID: payload-ref-...
Client stored ... serialized bytes; input reference: {"$daprRef":{...}}
Result: activity streamed ... serialized bytes using async chunks.
SHA-256 verified: ...
Serialized bytes include JSON structure, not just the document's characters.
Client inspection: fetching inputs/outputs at completion also resolves the referenced input on the client.
Takeaway: the workflow never reads the document; the activity streams bytes instead of deserializing it.

=== 4/5: Automatic references (pass document, receive reference) ===
Registration: PreferReference for WorkflowInput, with no size threshold.
...
Application code: pass DocumentPayload directly; the SAME streaming workflow receives PayloadReference<DocumentPayload>.
Reference creation: the sender's policy uploads the document; the receiver's type alone does not trigger it.
Workflow ID: payload-auto-ref-...
SDK stored the document automatically; input reference: {"$daprRef":{...}}
Result: activity streamed ... serialized bytes using async chunks.
SHA-256 verified: ...
...
Takeaway: the workflow never reads the document; the activity streams bytes instead of deserializing it.

=== 5/5: Explicit chunks (workflow and activity both stream) ===
Registration: Preserve with OffloadSites=None; no automatic boundary offloading.
...
Workflow: request 4 MiB chunks with context.ReadPayloadChunksAsync, checking the byte count.
The 128 KiB sample fits in one final partial chunk; the SDK owns the buffer and stream.
Activity: await foreach over resolver.ReadChunksAsync(input), checking the byte count and SHA-256.
Workflow ID: payload-chunks-...
Client stored the document; input reference: {"$daprRef":{...}}
Result: workflow and activity each streamed ... serialized bytes.
Activity SHA-256 verified: ...
Replay reads the workflow's chunks again; keep the referenced bytes immutable and available.
Client completion inspection also resolves the input; this is not a single-download example.
Takeaway: explicit chunk reads work in both workflow and activity code without deserializing the document.

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
curl.exe http://localhost:3500/v1.0/workflows/dapr/payload-auto-ref-REPLACE_WITH_ID
curl.exe http://localhost:3500/v1.0/workflows/dapr/payload-chunks-REPLACE_WITH_ID
```

Inline input/output contains the full document. Automatic input/output contains
reference envelopes, while SDK `ReadOutputAs<DocumentPayload>()` returns the
document and `ReadOutputAs<PayloadReference<DocumentPayload>>()` exposes the retained
wire reference. All three reference-input variations contain an input envelope, but
output is only the byte-count/hash summary. The explicit-reference and automatic-reference
variations differ in who requests the upload. The explicit-chunks variation also
reads the payload in the workflow, without changing the persisted representation.

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
disable readers for existing offloaded histories.

### Reading raw payload chunks

Activities and clients can inject `IWorkflowPayloadResolver` and enumerate stored bytes:

```csharp
await foreach (var chunk in resolver.ReadChunksAsync(reference, cancellationToken))
{
    // Consume this chunk before advancing; copy it if it must be retained.
    hash.AppendData(chunk.Span);
}
```

Opening is deferred until enumeration starts. Completing the loop, breaking out of it,
throwing, or cancellation disposes the download. `.WithCancellation(token)` is also
supported. Each enumeration reads storage again; chunks are not cached. Without an
explicit size, chunk sizes and boundaries are unspecified and may differ between implementations.

The built-in resolver's overload without a size exposes the binary store's chunks directly. Existing custom
resolvers inherit a default implementation that reads their `OpenAsync` stream with
a bounded buffer. `OpenAsync` remains available for APIs that consume `Stream`.
Orchestrators can use `context.ReadPayloadChunksAsync(reference)`; opening, reading,
and disposal use the worker's existing storage I/O queue.

Both streaming APIs return raw stored bytes, without deserialization, stage reversal,
or automatic integrity verification. The example verifies the hash and byte count
as it consumes the untransformed JSON payload.

To request a specific processing size without managing a stream, pass `chunkSize`:

```csharp
await foreach (var chunk in resolver.ReadChunksAsync(
    reference, chunkSize: 4 * 1024 * 1024, cancellationToken: cancellationToken))
{
    hash.AppendData(chunk.Span);
}
```

The SDK fills a reusable buffer, producing exactly 4 MiB per chunk except for the
final remainder. Empty payloads produce no chunks. The size must be positive;
validation is deferred until enumeration starts. Partial reads are combined internally,
and cancellation, early exit, and disposal remain SDK-managed. Larger chunks use more
buffer memory and delay delivery until that chunk is filled or the payload ends.
Consume or copy the memory before advancing; it may be reused.

The overload without `chunkSize` retains its existing unspecified chunk boundaries.
The requested size controls application-visible chunks, not storage/gRPC message sizes
or workflow/activity payload limits. Advanced users can still use `resolver.OpenAsync`
or `context.OpenPayloadAsync` when an API requires direct `Stream` access.
For existing calls that pass an untyped positional `default` as the second argument,
use `cancellationToken: default` to distinguish it from the new chunk-size overload.

### Explicit chunk reads inside a workflow and an activity

The fifth example stores the document from the client using `StoreAsync`, then
passes the reference into `ChunkStreamingWorkflow`. The workflow reads it explicitly:

```csharp
long byteCount = 0;
await foreach (var chunk in context.ReadPayloadChunksAsync(input, chunkSize: 4 * 1024 * 1024))
{
    byteCount += chunk.Length;
}
```

The workflow requests 4 MiB chunks; the demo's 128 KiB document produces only a final
partial chunk. Larger documents produce full 4 MiB chunks followed by any remainder.

After checking the byte count against `input.Size`, it passes the same reference to
`MeasurePayloadActivity`. That activity opens an independent enumeration:

```csharp
await foreach (var chunk in resolver.ReadChunksAsync(input))
{
    byteCount += chunk.Length;
    hash.AppendData(chunk.Span);
}
```

The activity verifies both size and SHA-256 before returning the small summary.
Neither consumer calls `ResolveAsync` or materializes `DocumentPayload`. The
client's upload still buffers serialization, and completion inspection hydrates
the input; this is not an end-to-end bounded-memory pipeline.

**Workflow reads must use the context extension**, not an injected resolver,
direct binary-store calls, or `Task.Run`. The SDK queues opening, reading, and
disposal outside deterministic execution. Do not use `ConfigureAwait(false)` in
the orchestration loop or schedule workflow actions based on chunk boundaries;
those boundaries can change between reads.

These reads are not durable activity results: enumeration runs again during
workflow replay. Keep the referenced bytes immutable and available for every replay.
The workflow checks only size in this example; the activity independently checks
content integrity. Workflow logic that depends on the bytes themselves must also
validate integrity as appropriate. Prefer the earlier activity-only variations
when the workflow does not need to inspect the data.

### Allocation behavior and optional payload byte caching

Automatic hydration buffers the downloaded UTF-8 bytes once and verifies their
hash before deserialization. The standard `JsonDaprSerializer`, including its
configured options and JSON converters, can deserialize those bytes directly.
Custom serializers, decorators, and subclasses continue to receive the original
string representation through `IDaprSerializer`; their behavior is not bypassed.
Outbound snapshots still capture values at the call boundary, including mutable
objects. Temporary upload buffers are pooled and cleared before reuse.

By default, each hydration reads storage again. History caching and
`ResolutionCacheSize` do not cache automatic inputs across orchestration executions.
Applications that treat referenced objects as immutable for their entire lifetime
can explicitly enable a process-local optimization **before registering/using
workflow payload services**:

```csharp
AppContext.SetSwitch("Dapr.Workflow.EnablePayloadByteCache", true);
```

This switch defaults to `false` and requires no new public SDK types or members.
The implementation retains only successfully integrity-verified serialized bytes,
not application objects. Every hydration still deserializes a fresh value, so
mutations do not leak between replays or activity calls. Streaming through
`OpenAsync` or `ReadChunksAsync` still reads storage directly.

Cancellation passed to a stream's `ReadAsync` also aborts an outstanding chunk
download. After cancellation, dispose the stream and open a new one to retry;
the canceled stream is not reusable. Disposal waits for an in-flight chunk
operation to finish before disposing its iterator. Concurrent reads on a single
payload stream are not supported.

The current cache is limited to 64 MiB of payload array capacity and 256 entries
per payload converter/service provider, with least-recently-used eviction and
one-minute absolute validity. These are implementation limits, not tuning APIs.
Concurrent cache misses may download independently; the budget bounds retained
payload arrays, not in-flight downloads.
Pooling and caching reduce cumulative allocations but can increase retained memory.
Reference metadata is part of cache identity; size limits and cancellation still
apply to cache hits. Reads with integrity verification disabled are not cached.
Writes always contact storage, even when identical bytes are cached locally.
`SkipWriteIfExists` accepts storage's `AlreadyExists` response; a local cache entry
is never evidence that an object still exists in shared storage. Manifest recording
is preserved.

The cache does not require replica affinity. A different worker/service provider
starts with a cold cache and retrieves and verifies the recorded reference from
storage. Cache hit rates therefore depend on routing, eviction, and worker lifetime.
Every replica's sidecar must access the same durable backing store, and referenced
objects must remain available for the lifetime of retained workflow history.
Independent sidecar-local in-memory stores, such as the demo component, are not
suitable authoritative storage for a replicated deployment.

`WorkflowPayloadCleaner` invalidates local entries before and after deletion,
including reads racing with cleanup. External deletion, overwrite, corruption, or
cleanup from another process may not be observed until the entry expires or is
evicted. **Leave the switch disabled if every read must observe current storage
state.** There is no distributed cache invalidation. The cache is not durable
storage, and references must remain available
for replay after eviction or process restart. Do not delete payloads while any
retained workflow history still needs them.

### Switch back to inline payloads

To stop automatic offloading while continuing to read old references, keep
`WithBinaryStorePayloads` on every participating client and worker and disable
automatic writes:

```csharp
services.AddDaprWorkflow()
    .WithBinaryStorePayloads(options =>
    {
        options.StoreName = "workflow-payloads";
        options.WritePreference = PayloadWritePreference.Preserve;
    });
```

`WritePreference` defaults to `Preserve`. Under this preference, ordinary serialized
payloads stay inline regardless of `OffloadSites` or `OffloadThreshold`. Existing
references are still resolved with the configured integrity and size checks.
This is a host-wide write policy, not a per-instance or version selector: future
writes from running workflows also become inline. Existing history is unchanged.
`OffloadSites = PayloadKind.None` remains an alternative for disabling all automatic
offload boundaries. Inline payloads must fit the runtime's normal payload limits.

For workflows already using ordinary inputs and outputs, such as
`DocumentProcessingWorkflow`, no contract change is needed.
`AutomaticReferencePayloadExample`, however, relies on automatic input offloading:
switching it to `Preserve` or disabling `WorkflowInput` would send inline documents
to a reference-typed receiver and fail. To retain that contract under `Preserve`,
explicitly store the input as in `ReferencePayloadExample`.

For explicit references,
the switch intentionally does **not** turn `PayloadReference<T>` into `T` or disable
`IWorkflowPayloadResolver.StoreAsync`; doing so would break existing contracts.
To move those workflows back to inline data:

1. Add a new workflow version using ordinary `T` inputs/results and ordinary-payload
   activities instead of reference-typed ones. Remove explicit `StoreAsync` calls
   from that new path and pass values directly when scheduling it.
2. Route new instances to that version with automatic offloading disabled. Keep
   the previous workflow version and its reference-typed activities registered so
   existing instances replay with their original contracts.
3. Retain `WithBinaryStorePayloads`, storage access, and stored bytes for as long as
   any retained history, result, or old instance needs them.

Do not replace a reference-typed workflow or activity in place under the same
registered name. The option does not add versioning patches or migrate stored data.

### Write preferences and cross-app calls

| `PayloadWritePreference` | New outgoing top-level payloads |
|---|---|
| `Preserve` (default) | Ordinary values stay inline; references remain references. |
| `PreferReference` | Ordinary values meeting `OffloadThreshold` at enabled `OffloadSites` become references. |
| `PreferInline` | Ordinary values stay inline; references are resolved to the original serialized content. |

These preferences govern future writes, not retained history or instance-wide policy.
Under `Preserve`, a reference read as an ordinary `T` and returned as `T` is a new inline
value. Explicit `StoreAsync` still writes under every preference.

`PreferInline` is not safe for receivers expecting `PayloadReference<T>`. When keeping
old reference-typed versions alongside a new ordinary-payload version, use `Preserve`
and remove explicit reference creation from the new version. Do not globally inline
references still needed by old contracts.

Cross-app inputs require inline data by default even under `PreferReference`. The SDK
resolves outgoing references before submitting the activity, child, or detached call.
Known reference-aware destinations can opt out per call:

```csharp
await context.CallActivityAsync<int>("MeasureReferenceActivity", reference,
    new WorkflowTaskOptions(TargetAppId: "document-service", AllowPayloadReferences: true));
```

Child calls use `ChildWorkflowTaskOptions`. The detached overload accepts
`ScheduleNewDetachedWorkflowAsync(name, instanceId, input, startTime, options)`, with
`WorkflowTaskOptions` for routing and payload permission; retries and history propagation
are not supported for detached calls. An explicit same-app target follows the normal
write preference.

Permission is not capability negotiation: currently only .NET supports these envelopes,
and the destination must also register payload support and access the same stored objects.
Inlining requires sender-side storage access, respects `MaxInMemoryRehydrationSize` and
integrity settings, and fails explicitly on unsupported envelopes, inaccessible data,
corruption, or exceeded limits. It never falls back to sending a reference.
Runtime/transport limits and message overhead still apply after materialization.

Inbound ordinary `T` contracts accept either representation regardless of write preference.
Reference-typed inputs receive references without automatic resolution, but reject inline
values rather than uploading them. Decoding does not change history. The outbound
cross-app rule covers only top-level call inputs: nested references, return values, and
propagated history need separate compatibility planning.

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
