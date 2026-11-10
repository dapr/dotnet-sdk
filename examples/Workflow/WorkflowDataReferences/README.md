# Workflow data references: choose the right boundary APIs

These runnable examples separate three choices that are easy to conflate:

1. **Storage:** should a value travel inline in workflow history or through a binary-store reference?
2. **Receiving contract:** does workflow/activity code need a materialized `T`, or a `PayloadReference<T>` / `PayloadReference`?
3. **Consumption:** does a reader need the whole object, a `Stream`, or incremental byte chunks?

**Automatic offloading is not the same as streaming.** It can shrink history while
still serializing and materializing entire objects. For a source that is not already
in memory, start with the **`explicit-stream`** or **`auto-stream`** examples.

## Pick an example

The default run follows this order. Each scenario prints its name, intended use,
workflow ID, and result. The links lead to the actual registration and client calls.

| Name / source | Client sends | Workflow/activity behavior | Client reads |
|---|---|---|---|
| [inline](Examples/InlinePayloadExample.cs) | An ordinary document | Ordinary objects; serialized values stay in history | A materialized document |
| [auto-values](Examples/AutomaticPayloadExample.cs) | The same document | The same ordinary-object code; large values are automatically offloaded at all four boundaries | A materialized document; references are hydrated |
| [auto-output-chunks](Examples/AutomaticOutputChunksExample.cs) | An ordinary document | The same ordinary-object code; all four payload boundaries always offload | Raw serialized JSON chunks, without client-side hydration |
| [explicit-reference](Examples/ReferencePayloadExample.cs) | A reference from `StoreAsync(document)` | Workflow forwards a typed reference; activity streams serialized bytes and returns an inline summary | A typed summary; normal metadata retrieval also hydrates the input |
| [auto-reference](Examples/AutomaticReferencePayloadExample.cs) | An ordinary document, always offloaded on scheduling | The same reference-typed workflow/activity as above | The same typed summary and metadata caveat |
| [explicit-stream](Examples/StreamUploadPayloadExample.cs) | A reference from `StoreStreamAsync(source)` | Untyped references at all four boundaries; activity stores its output stream explicitly | Raw binary output chunks |
| [auto-stream](Examples/StreamUploadPayloadExample.cs) | A `Stream` directly to scheduling | The same reference-typed workflow/activity; SDK uploads input before scheduling | Raw binary output chunks; no client resolver calls |
| [workflow-chunks](Examples/ChunkStreamingPayloadExample.cs) | A reference from `StoreAsync(document)` | **Advanced:** the workflow itself enumerates chunks, then an activity independently reads them | An inline summary; replay can repeat workflow downloads |

Object examples begin with an already-materialized 128 KiB text document. Stream
examples generate a 32 MiB binary file using a reusable 64 KiB buffer. These examples
compare API behavior, not performance or total download counts.

## Run just the scenario you need

Prerequisites: .NET 10 SDK, Dapr CLI, a local Dapr runtime with binary-store support
(1.19+, also supported by `1.19.0-rc.3`), Redis, placement, and scheduler.
The supplied state-store component assumes Redis at `localhost:6379`.
Stream examples also need temporary disk space for the source and SDK staging files.

From this directory, list scenarios without starting a sidecar:

```powershell
dotnet run --framework net10.0 -- --List=true
```

In one terminal, start a **dedicated** sidecar:

```powershell
dapr run --app-id workflow-data-references --dapr-http-port 3500 --dapr-grpc-port 50001 --resources-path .\components
```

In another terminal:

```powershell
# Recommended first stop for a source too large to materialize.
dotnet run --framework net10.0 -- --Example=auto-stream

# Compare ordinary typed reads with chunked reads of automatically offloaded output.
dotnet run --framework net10.0 -- --Example=auto-values
dotnet run --framework net10.0 -- --Example=auto-output-chunks

# Walk through all eight scenarios.
dotnet run --framework net10.0 -- --Example=all

# Backward-compatible inline-only mode: no payload support is registered.
dotnet run --framework net10.0 -- --DataRefs:Enabled=false
```

Omitting `--Example` runs all scenarios unless `DataRefs:Enabled=false`. Invalid or
disabled selections fail with a message. `--List=true` shows the available names.
For a different sidecar port, set `DAPR_GRPC_ENDPOINT` for the application.
If placement/scheduler use non-default addresses, supply the matching Dapr CLI flags.

[Program.cs](Program.cs) starts a separate host for each selected policy and stops
the previous worker before starting the next. Do not attach another worker to this
app ID while running the examples. The project includes the SDK source generator,
so `AddDaprWorkflow()` discovers the workflows and activities; registration code
in each example focuses on the payload policy.
The local project explicitly references the discovery generator because project
references do not automatically import the generators bundled in the NuGet package.

## Understand the four payload boundaries

```text
client --(1: workflow input)--> workflow
                                  |
                            (2: activity input)
                                  v
                               activity
                                  |
                            (3: activity output)
                                  v
                               workflow --(4: workflow output)--> stored result
                                                                      |
                                                         client chooses a read API
```

`OffloadSites` controls automatic **writes** at each sending boundary:
`WorkflowInput`, `ActivityInput`, `ActivityOutput`, and `WorkflowOutput`.
Enabling only `WorkflowInput` does not automatically offload activity or workflow output.
An existing reference can still be forwarded without enabling automatic writes.

The **receiving type** controls whether that reference becomes a value:

| Receiver needs | Contract / API | Memory behavior |
|---|---|---|
| A document object | Workflow/activity parameter `DocumentPayload` | SDK hydrates a reference before invoking user code |
| A handle to serialized document bytes | `PayloadReference<DocumentPayload>` | Reference only; `T` describes the stored serialization |
| A handle to arbitrary binary bytes | `PayloadReference` | Reference only; no assumed JSON/document format |
| A materialized value from a reference | `resolver.ResolveAsync<T>(reference)` | Retrieves/deserializes the value |
| A stream from a reference | `resolver.OpenAsync(reference)` | Incremental raw-byte reads; caller disposes the stream |
| Chunks from a reference | `resolver.ReadChunksAsync(reference[, chunkSize])` | Incremental raw bytes; enumeration owns the download |
| Workflow output chunks without obtaining a reference first | `client.ReadWorkflowOutputChunksAsync(instanceId[, chunkSize])` | Opens only referenced output; bypasses payload hydration |

Declaring `PayloadReference<T>` does **not** make an inline value become a reference.
The sender must explicitly store it or enable automatic offloading. Use
`OffloadThreshold = null` when a reference-typed receiver must work even for small values.
Do not label arbitrary binary bytes with `.AsTyped<DocumentPayload>()`; that does
not convert them into a serialized document.

## Ordinary values: inline, automatically offloaded, or read as chunks

`inline`, `auto-values`, and `auto-output-chunks` share
[DocumentProcessingWorkflow](Workflows/DocumentProcessingWorkflow.cs) and
[UppercaseDocumentActivity](Activities/UppercaseDocumentActivity.cs).
The workflow and activity use `DocumentPayload` for both input and output.
They still allocate full objects and strings even when history contains references.

- **inline:** `AddDaprWorkflow()` without payload support. Use normal typed output reads.
- **auto-values:** `PreferReference`, all four offload sites, a 64 KiB threshold.
  Small values stay inline. The sample document exceeds the threshold at every boundary.
- **auto-output-chunks:** the same sending sites, but `OffloadThreshold = null`
  guarantees an output reference. The client waits without input/output values and
  reads chunks instead of deserializing a document.

Normal typed retrieval is intentionally simple:

```csharp
var state = await client.WaitForWorkflowCompletionAsync(
    instanceId, getInputsAndOutputs: true, cancellation: cancellationToken);
if (state.RuntimeStatus != WorkflowRuntimeStatus.Completed)
    throw new InvalidOperationException($"Workflow ended as {state.RuntimeStatus}.");
var output = state.ReadOutputAs<DocumentPayload>();
```

The asynchronous metadata call prepares referenced values **before** the synchronous
`ReadOutputAs<T>()`. Asking for `PayloadReference<T>` afterward does not undo that
download. `MaxInMemoryRehydrationSize` limits buffered resolution; it is not a switch
to turn ordinary metadata reads into a streaming API.

## Object inputs with reference-typed consumers

`explicit-reference` and `auto-reference` share
[StreamingPayloadWorkflow](Workflows/StreamingPayloadWorkflow.cs) and
[MeasurePayloadActivity](Activities/MeasurePayloadActivity.cs).
The workflow forwards a `PayloadReference<DocumentPayload>` without inspecting its
contents. The activity enumerates the serialized bytes, verifies size/hash, and
returns a small inline `PayloadSummary`.

The difference is the upload call:

```csharp
// explicit-reference: Preserve + OffloadSites=None.
// StoreAsync serializes/buffers an object and writes regardless of automatic thresholds.
var reference = await resolver.StoreAsync(document, instanceId, cancellationToken);
await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, reference,
    startTime: null, cancellation: cancellationToken);

// auto-reference: PreferReference + WorkflowInput + OffloadThreshold=null.
// The SDK creates the reference before scheduling; the receiver still declares PayloadReference<T>.
await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, document,
    startTime: null, cancellation: cancellationToken);
```

These are **alternatives**, not two schedules to perform for the same instance.
Neither is a bounded-memory upload from an unmaterialized source.
The summary is inline, so output chunk reads are inappropriate. The sample's typed
summary inspection also hydrates its referenced input on the client; only the
workflow/activity handling avoids materializing that document.

## Unmaterialized sources: explicit or automatic stream upload

Both stream scenarios live in
[StreamUploadPayloadExample](Examples/StreamUploadPayloadExample.cs).
The constructor's `automaticReferences` flag changes only the client upload and
sending policy; the workflow, activity, and chunked client read are identical.

```csharp
await using var source = File.OpenRead("large-file.bin");

// explicit-stream: Preserve + OffloadSites=None.
PayloadReference input = await resolver.StoreStreamAsync(source, instanceId, cancellationToken);
await client.ScheduleNewWorkflowAsync(nameof(StreamUploadWorkflow), instanceId, input,
    startTime: null, cancellation: cancellationToken);
```

Or, instead:

```csharp
// auto-stream: WithBinaryStorePayloads + PreferReference + WorkflowInput.
await using var source = File.OpenRead("large-file.bin");
await client.ScheduleNewWorkflowAsync(nameof(StreamUploadWorkflow), instanceId, source,
    startTime: null, cancellation: cancellationToken);
```

The automatic path recognizes a **top-level `Stream` on initial scheduling** and
always uploads it, including small/empty streams; the normal object threshold does
not apply. Without the required policy it rejects the stream rather than serializing
the `Stream` object. This shortcut does not apply to nested streams, activity inputs
or outputs, workflow returns, events, or rerun overrides.

### Persisting activity output and returning workflow output

[CopyStreamPayloadActivity](Activities/CopyStreamPayloadActivity.cs) uses the explicit
write API in **both** scenarios:

```csharp
await using var content = await resolver.OpenAsync(input, cancellationToken);
return await resolver.StoreStreamAsync(content, context.InstanceId, cancellationToken);
```

In an application, `content` could instead be a transformed, generated, or HTTP
response stream. This sample copies identical bytes and checks size/hash; identical
content within the same instance reuses its content-addressed key.

[StreamUploadWorkflow](Workflows/StreamUploadWorkflow.cs) simply forwards references:

```csharp
public override Task<PayloadReference> RunAsync(WorkflowContext context, PayloadReference input)
    => context.CallActivityAsync<PayloadReference>(nameof(CopyStreamPayloadActivity), input);
```

No live `Stream` enters workflow history. The workflow never uploads or downloads
the bytes. Create new references in clients/activities, not in replayed orchestrator code.

### Upload lifetime and resource costs

- Reads from the current stream position to EOF; partial reads, non-seekable sources,
  unknown lengths, and empty streams work. The caller owns the source and must leave
  it open until the upload/scheduling call completes.
- Leaves the source open and consumed, even on failure. Reopen or rewind before retrying;
  do not access the source concurrently with an upload.
- Uses a bounded 64 KiB staging buffer and temporary disk proportional to payload size
  to compute the content-addressed key **before** upload. This is not a simultaneous
  source-to-network pipe. Staging files are deleted on disposal, including errors/cancellation.
- Records the manifest after upload. A later manifest or scheduling failure can leave
  stored bytes; upload is not rolled back automatically.
- Stores raw bytes without JSON conversion. `StoreStreamAsync` is independent of
  automatic thresholds/sites and rehydration limits. Preserve references when forwarding
  arbitrary binary data; do not select a policy that inlines them.

## Reading output a few chunks at a time

The three chunked-client scenarios use this pattern:

```csharp
var state = await client.WaitForWorkflowCompletionAsync(
    instanceId, getInputsAndOutputs: false, cancellation: cancellationToken);
if (state.RuntimeStatus != WorkflowRuntimeStatus.Completed)
    throw new InvalidOperationException($"Workflow ended as {state.RuntimeStatus}.");

await foreach (var chunk in client.ReadWorkflowOutputChunksAsync(
    instanceId, chunkSize: 64 * 1024, cancellationToken: cancellationToken))
{
    await ProcessChunkAsync(chunk, cancellationToken); // Your processing, not an SDK method.
    if (HaveEnoughData())                            // Your stopping condition.
        break;
}
```

Both `DaprWorkflowClient` and `IDaprWorkflowClient` expose this API. Omit `chunkSize`
to use native transport chunks. With a size, the SDK fills a reusable buffer of that
size, except for the final partial chunk. It does not change transport message sizes.

The runnable examples stop after a preview chunk, then demonstrate a **separate**
full pass. Each enumeration starts a new metadata lookup/download from the beginning,
not at the previous stopping point. Only the full verification pass in the binary
examples needs to consume all bytes.

Consume or copy each chunk before advancing; memory may be reused. Do not collect
all chunks if bounded memory matters. Chunk boundaries are not JSON records and can
split UTF-8 characters. Automatically offloaded document output is serialized JSON;
`StoreStreamAsync` output is the raw bytes originally written.

Enumeration owns the download. Breaking, cancellation, or a consumer exception
disposes it; transport may have prefetched some data. Reads are lazy and operate on
**completed stored output**, not live output from a running workflow. Chunk reads
do not deserialize, reverse payload stages, or automatically verify integrity.

### What if the output is not a reference?

The API looks at the **persisted output**, not whether the application calls its mode
"automatic" or "explicit."

| Situation | Behavior / action |
|---|---|
| Referenced output, including an empty stored payload | Stream it; no application-side resolver call needed |
| Inline or absent output | `InvalidOperationException`; use ordinary typed reads or change the producer to persist a reference |
| Automatic offloading left a small output inline | Same rejection; enable `WorkflowOutput` and use `OffloadThreshold=null` if every output must be stream-readable |
| Payload support missing on the client | `InvalidOperationException` before lookup; normally register `WithBinaryStorePayloads` |
| Workflow missing or not successfully completed | `InvalidOperationException`; there is no implicit wait |
| Referenced bytes deleted | `PayloadNotFoundException`; keep bytes available for readers |
| Malformed reference or storage/transport failure | Error propagates; no empty-result or buffer-and-slice fallback |

Reads of existing references work regardless of the current write preference or
offload sites. Enabling automatic output offloading does not make an object-producing
workflow a streaming writer; use the activity's `StoreStreamAsync` for that.

**Metadata caveats:** ordinary get/wait APIs are unchanged. Even with
`getInputsAndOutputs: false`, a referenced custom status may still hydrate during
a wait; these examples keep status small and inline or do not set it. The chunk API
itself bypasses all input/output/custom-status reference hydration. However, Dapr's
metadata RPC returns their wire values together: large **inline** fields still travel
over that RPC, including inline output before rejection. Reference-backed output
downloads are incremental; this is not bounded-memory retrieval of inline metadata.

## Advanced: reading inside the workflow

[ChunkStreamingWorkflow](Workflows/ChunkStreamingWorkflow.cs) uses
`context.ReadPayloadChunksAsync(input, chunkSize: 32 * 1024)`. It checks the byte
total, then calls the measuring activity, which independently reads the data.
The sample spans multiple chunks.

Use the context API in orchestration, not a directly injected resolver/storage
client. The SDK queues reads outside deterministic execution, but replay reads the
stored bytes again. Keep them immutable and available, and do not let chunk boundaries
or timing drive orchestration decisions. Prefer forwarding references to activities
when the workflow only needs the result of processing.

## Inspect and retain the data

Each run leaves workflow history and prints an instance ID. Inspect one using:

```powershell
curl.exe http://localhost:3500/v1.0/workflows/dapr/REPLACE_WITH_INSTANCE_ID
```

Workflow history/actor state (`components/statestore.yaml`) and payload bytes
(`components/binarystore.yaml`) are separate stores. The demo binary store is in-memory:
stopping the sidecar loses its bytes even if Redis retains history. Bounded SDK buffers
do not imply bounded storage-component memory. The component explicitly permits 64 MiB
per file and 256 MiB in total so the two 32 MiB stream scenarios fit; these are storage
limits, not SDK chunk sizes. Repeated runs retain data and can eventually reach the
total limit.

Production cleanup must account for readers, retries, and retained-history replays.
Use the configured payload manifest/`WorkflowPayloadCleaner` only when those references
are no longer needed; do not delete data just because the first client finished reading.
Cross-app reference calls additionally require `AllowPayloadReferences = true` and
destination access to the same stored bytes.
