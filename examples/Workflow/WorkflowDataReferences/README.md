# Workflow data references: three everyday recipes

Start with ordinary objects, uploading incoming bytes, or reading referenced output.
The default run demonstrates these three recipes, in that order. Existing comparison
scenarios are still available individually or with `--Example=all`.

### Pass ordinary objects

Run `--Example=auto-values`. Configure automatic offloading once, then keep ordinary
workflow/activity contracts and client calls:

```csharp
await client.ScheduleNewWorkflowAsync(nameof(DocumentProcessingWorkflow), instanceId, document,
    startTime: null, cancellation: cancellationToken);
var state = await client.WaitForWorkflowCompletionAsync(
    instanceId, getInputsAndOutputs: true, cancellation: cancellationToken);
if (state.RuntimeStatus != WorkflowRuntimeStatus.Completed)
    throw new InvalidOperationException($"Workflow ended as {state.RuntimeStatus}.");
var output = state.ReadOutputAs<DocumentPayload>();
```

See [AutomaticPayloadExample](Examples/AutomaticPayloadExample.cs) for registration.
Offloading reduces history size; ordinary object inputs and outputs still materialize.

### Upload incoming bytes

Run `--Example=direct-write`. Inside an activity, choose **one** overload for your
application's source; all three upload immediately and return a `PayloadReference`:

```csharp
return await context.UploadPayloadAsync(sourceStream);

return await context.UploadPayloadAsync(byteChunks);

return await context.UploadPayloadAsync(
    (destination, ct) => WritePagesAsync(destination, ct));
```

`sourceStream`, `byteChunks`, and `WritePagesAsync` represent your file, external
data source, or producer code. No resolver injection or instance-ID plumbing is
needed. The activity context supplies its instance ID and worker-shutdown token.
Keep the source stream open until upload completes and dispose it yourself; the
SDK owns the callback destination. The workflow only forwards the returned reference.

[WritePagedPayloadActivity](Activities/WritePagedPayloadActivity.cs) is the runnable
chunk example. Synthetic page generation and full-read integrity verification live
in [SamplePayloadData](SamplePayloadData.cs), outside the main API walkthrough.
Streaming reads do **not** automatically verify hashes; that helper does so explicitly.

### Read referenced output

Run `--Example=auto-output-chunks`. After waiting for successful completion with
`getInputsAndOutputs: false`, consume output without hydrating a document:

```csharp
await foreach (var chunk in client.ReadWorkflowOutputChunksAsync(instanceId, cancellationToken))
{
    await ProcessChunkAsync(chunk, cancellationToken); // Your processing code.
}
```

Consume each chunk before advancing; memory may be reused. You can `break` early
to close the download, but a partial read cannot verify whole-payload integrity.
For metadata first, use `GetWorkflowOutputReferenceAsync`; then choose
`resolver.OpenAsync(reference)` or `resolver.ReadChunksAsync(reference)`.
The direct-write recipe demonstrates this alternative.

## Start with your task

| I need to... | Start here | Cost / contract |
|---|---|---|
| Pass a large object without enlarging workflow history | `auto-values`, or `StorePayloadAsync(value)` for explicit references | Serializes/materializes a complete value; returns `PayloadReference<T>` when explicit |
| Upload bytes or pages while receiving them | `direct-write` and `UploadPayloadAsync(source)` | Bounded buffering, no staging file; raw bytes and a unique-key `PayloadReference` |
| Read only the bytes I need from a completed result | `ReadWorkflowOutputChunksAsync`, or `GetWorkflowOutputReferenceAsync` then `OpenAsync` / `ReadChunksAsync` | No payload hydration; caller disposes streams, enumeration disposes downloads |

Use the **activity context** inside activities, the **injected resolver** in clients
and shared services, and the **workflow context** only for read-only access during
orchestration. Activities supply their instance ID automatically. Workflows cannot
upload payloads.

The representations mean different things:

| Representation | Meaning |
|---|---|
| `T` / `PayloadReference<T>` | A complete value in the configured serializer's format |
| `Stream` | Raw bytes, not automatically serialized records |
| `IAsyncEnumerable<ReadOnlyMemory<byte>>` | Raw bytes in reusable chunks, not record or page boundaries |

`AsTyped<T>()` declares an interpretation; it does not convert or validate bytes.
Use explicit framing such as NDJSON for a sequence of records. A byte chunk can
split a UTF-8 character or JSON record.

### Choose the write behavior, not just the input shape

| Activity context | Injected resolver | Storage behavior |
|---|---|---|
| `StorePayloadAsync(value)` | `StoreAsync(value, instanceId)` | Serializes an object; content-addressed storage |
| `StorePayloadStreamAsync(source)` | `StoreStreamAsync(source, instanceId)` | Stages the entire stream to temporary disk, then uploads under its hash |
| `UploadPayloadAsync(source)` | `UploadAsync(source, instanceId)` | Uploads immediately; accepts a readable stream, async byte chunks, or a writer callback |

All three `Upload` overloads have identical hashing, unique-key, backpressure, and
failure semantics. They do not deduplicate retries. `WritePayloadAsync` / `WriteAsync`
remain compatible callback entry points; prefer `Upload` for new code.
Do not pass streams or byte sequences to the generic object-storage API and expect
streaming behavior.

<details>
<summary>Full example catalog and advanced comparisons</summary>

The default recipes are `auto-values`, `direct-write`, and `auto-output-chunks`.
Every scenario prints its name, intended use, workflow ID, and result.
The links lead to the actual registration and client calls.

| Name / source | Client sends | Workflow/activity behavior | Client reads |
|---|---|---|---|
| [inline](Examples/InlinePayloadExample.cs) | An ordinary document | Ordinary objects; serialized values stay in history | A materialized document |
| [auto-values](Examples/AutomaticPayloadExample.cs) | The same document | The same ordinary-object code; large values are automatically offloaded at all four boundaries | A materialized document; references are hydrated |
| [auto-output-chunks](Examples/AutomaticOutputChunksExample.cs) | An ordinary document | The same ordinary-object code; all four payload boundaries always offload | Raw serialized JSON chunks, without client-side hydration |
| [explicit-reference](Examples/ReferencePayloadExample.cs) | A reference from `StoreAsync(document)` | Workflow forwards a typed reference; activity streams serialized bytes and returns an inline summary | A typed summary; normal metadata retrieval also hydrates the input |
| [auto-reference](Examples/AutomaticReferencePayloadExample.cs) | An ordinary document, always offloaded on scheduling | The same reference-typed workflow/activity as above | The same typed summary and metadata caveat |
| [explicit-stream](Examples/StreamUploadPayloadExample.cs) | A reference from `StoreStreamAsync(source)` | Untyped references at all four boundaries; activity stores its output stream explicitly | Raw binary output chunks |
| [auto-stream](Examples/StreamUploadPayloadExample.cs) | A `Stream` directly to scheduling | The same reference-typed workflow/activity; SDK uploads input before scheduling | Raw binary output chunks; no client resolver calls |
| [direct-write](Examples/DirectWritePayloadExample.cs) | A page count | Activity uploads async byte chunks without temporary files | Output reference without hydration, then sample-only verification in a helper |
| [tenant-prefix](Examples/TenantPrefixPayloadExample.cs) | One page, for each of two tenant-qualified instance IDs | The same upload workflow/activity as direct-write; a prefix factory routes each tenant's bytes | Output references only; verifies and prints each tenant/instance path |
| [workflow-chunks](Examples/ChunkStreamingPayloadExample.cs) | A reference from `StoreAsync(document)` | **Advanced:** the workflow itself enumerates chunks, then an activity independently reads them | An inline summary; replay can repeat workflow downloads |

Object examples begin with an already-materialized 128 KiB text document. Stream
examples generate 32 MiB using a reusable 64 KiB buffer (direct-write generates pages, not a file).
The tenant-prefix example needs only one 64 KiB page per tenant. These examples
compare API behavior, not performance or total download counts.

</details>

## Run just the scenario you need

Prerequisites: .NET 10 SDK, Dapr CLI, a local Dapr runtime with binary-store support
(1.19+, also supported by `1.19.0-rc.3`), Redis, placement, and scheduler.
The supplied state-store component assumes Redis at `localhost:6379`.
The `explicit-stream` and `auto-stream` examples also need temporary disk space for
the source and SDK staging files. `direct-write` does not use temporary files.

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
# Run the three introductory recipes (also the default when no Example is given).
dotnet run --framework net10.0 -- --Example=recipes

# Recommended first stop for a source too large to materialize.
dotnet run --framework net10.0 -- --Example=direct-write

# Compare content-addressed uploads that stage before uploading.
dotnet run --framework net10.0 -- --Example=auto-stream

# Compare ordinary typed reads with chunked reads of automatically offloaded output.
dotnet run --framework net10.0 -- --Example=auto-values
dotnet run --framework net10.0 -- --Example=auto-output-chunks

# See the same workflow store data beneath two different tenant prefixes.
dotnet run --framework net10.0 -- --Example=tenant-prefix

# Include all ten scenarios and advanced comparisons.
dotnet run --framework net10.0 -- --Example=all

# Backward-compatible inline-only mode: no payload support is registered.
dotnet run --framework net10.0 -- --DataRefs:Enabled=false
```

Omitting `--Example` runs the three recipes unless `DataRefs:Enabled=false`. Invalid or
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

## Advanced: content-addressed staging and deduplication

Use `UploadPayloadAsync` / `UploadAsync` for immediate uploads. These older stream
scenarios deliberately keep `StoreStreamAsync` to demonstrate a different tradeoff:
temporary-file staging enables content-addressed keys and deduplication within an
instance. Replacing it with `UploadAsync` would change that behavior.

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
await using var content = await context.OpenPayloadAsync(input);
return await context.StorePayloadStreamAsync(content);
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

## Activity-context operations and direct streaming writes

Activities no longer need to inject `IWorkflowPayloadResolver`. With payload support
registered, their context exposes `ResolvePayloadAsync`, `OpenPayloadAsync`,
`ReadPayloadChunksAsync` (including a sized overload), `StorePayloadAsync`,
`StorePayloadStreamAsync`, `UploadPayloadAsync`, and `HasPayloadResolver`.
Writes automatically use `context.InstanceId`. When no cancelable token is supplied,
methods use `context.CancellationToken`, which signals **worker shutdown**, not
workflow termination or an activity timeout. An explicit cancelable token replaces
that default; link it with the context token if both should stop the operation.
The injected resolver remains available to clients and shared application services.

The [upload recipe](#upload-incoming-bytes) shows the three overloads together.
For HTTP sources, request an unbuffered response with
`HttpCompletionOption.ResponseHeadersRead`, obtain its content stream, and pass it
directly to `UploadPayloadAsync`. Keep the response and stream alive until upload
finishes; dispose them afterward.

Clients can call `resolver.UploadAsync(source, instanceId, cancellationToken)`
with the same three input forms. Typed reads infer the type:
`resolver.ResolveAsync(typedReference, cancellationToken)`.
The source remains caller-owned. The destination is SDK-owned, write-only and
non-seekable: do not dispose or retain it, and do not write concurrently.
Finish and flush encoding/compression wrappers before the callback returns, using
`leaveOpen: true`. Honor the callback token, including when fetching the next page.

Unlike `StoreStreamAsync`, this uploads **while producing**. A bounded pipe
applies backpressure, including when a single write is much larger than its buffer.
The SDK hashes and counts bytes as the uploader reads them; it neither materializes
the payload nor creates a staging file. Actual remote persistence timing depends
on the binary-store component.

The storage key is unique and chosen before upload; SHA-256 and byte count are
recorded separately in the unchanged reference envelope after upload completes.
Identical content and activity retries create different keys; `SkipWriteIfExists`
does not deduplicate this path. Existing content-addressed APIs are unchanged.

Producer/upload failures cancel the other side. The operation returns a reference
only after producer completion, upload acknowledgement, and manifest recording.
On failure it attempts deletion with an independent, bounded cleanup token;
cleanup errors are reported alongside the original error. This is not transactional:
process termination or ambiguous remote failures can leave orphaned objects.
Successful uploads are retained even if later activity-result delivery fails.

Return the reference, not the stream, and preserve it across activity/workflow
output boundaries. Bytes are raw, not automatically serialized. Encode paged
records explicitly (for example NDJSON); concatenated JSON documents are not one
ordinary JSON value. Use a typed reference only for a complete payload matching
the configured serializer. Orchestrators cannot use the writer API.

## Advanced configuration: tenant and instance prefixes

Every new payload belongs to an instance. `KeyPrefix` is the shared root; optionally
derive a relative prefix from the instance ID. This is independent of which upload
overload you choose, so it is kept separate from the basic upload recipe.

Run `--Example=tenant-prefix` to upload one page for each of `acme` and `contoso`.
[TenantPrefixPayloadExample](Examples/TenantPrefixPayloadExample.cs) reuses the
direct-write workflow and activity unchanged, then retrieves only the output
references to verify and print their paths. It configures:

```csharp
options.KeyPrefix = "workflow-payloads";
options.InstancePrefixFactory = instanceId =>
{
    // Application convention: alphanumeric tenant followed by "--" and an execution ID.
    var separator = instanceId.IndexOf("--", StringComparison.Ordinal);
    if (separator <= 0 || !instanceId[..separator].All(char.IsAsciiLetterOrDigit))
        throw new ArgumentException("Expected an alphanumeric tenant followed by '--' in the instance ID.");
    return $"tenants/{instanceId[..separator]}/instances";
};
```

Schedule with a matching ID, for example `var instanceId = $"acme--{Guid.NewGuid():N}";`.
The activity and workflow need no tenant-specific code.

For tenant `acme` and instance `acme--run1`, keys become:

```text
workflow-payloads/tenants/acme/instances/acme--run1/{sha256}
workflow-payloads/tenants/acme/instances/acme--run1/uploads/{uniqueId}
```

The factory runs once per write, for automatic offloading and every explicit write
API. It returns a non-empty relative prefix, **not** a complete key. The SDK appends
the instance ID and controls the payload identifier. Without a factory, the layout
remains `{KeyPrefix}/{instanceId}/{payloadKey}` for ordinary IDs.

Prefixes reject absolute paths, empty/dot/parent segments, control characters, `%`,
and reserved characters such as `\`, `:`, `*`, `?`, `[`, `]`, and `#`. The instance ID is
percent-encoded as one path segment: for example `a/b` becomes `a%2Fb`. Existing
references with older unescaped keys remain readable, but new writes for such IDs
use encoded keys rather than deduplicating against the old paths.

The factory must be thread-safe and return consistent results across clients,
workers, and retries. Missing tenant mappings should throw, not fall back to another
tenant. Do not depend on request-scoped/ambient HTTP tenant state. Child workflows
need IDs that follow your convention too; tenant metadata is not automatically
propagated. Prefixes organize data and do not enforce authorization.

Reads and manifest-based cleanup use the reference's recorded store and key; they
never reevaluate the factory. Prefix changes do not rewrite historical references.

## Retrieve the reference without downloading its contents

```csharp
var reference = await client.GetWorkflowOutputReferenceAsync(instanceId, cancellationToken);
// Choose how much to read only after obtaining the reference:
await using var source = await resolver.OpenAsync(reference, cancellationToken);
```

This requires a successfully completed workflow whose persisted output is a reference.
It does not wait, hydrate input/output/custom-status references, access the binary
store, or require payload registration merely to retrieve the reference. Inline or
absent output is an explicit error. The metadata RPC still transfers inline wire
values, so this is not a bounded-memory API for arbitrarily large inline metadata.

Contrast this with `GetWorkflowStateAsync` / ordinary typed metadata reads, which may
hydrate before `ReadOutputAs<T>()` is called. Requesting a reference from that state
afterward cannot undo the download.

## Reading output a few chunks at a time

The chunked-client scenarios use this pattern:

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

The advanced `explicit-stream` and `auto-stream` examples stop after a preview
chunk, then demonstrate a **separate** full pass. Each enumeration starts a new
metadata lookup/download from the beginning, not at the previous stopping point.
The introductory `auto-output-chunks` recipe uses just one pass to count the bytes.

Consume or copy each chunk before advancing; memory may be reused. Do not collect
all chunks if bounded memory matters. Chunk boundaries are not JSON records and can
split UTF-8 characters. Automatically offloaded document output is serialized JSON;
`StoreStreamAsync` output is the raw bytes originally written.

Enumeration owns the download. Breaking, cancellation, or a consumer exception
disposes it; transport may have prefetched some data. Reads are lazy and operate on
**completed stored output**, not live output from a running workflow. Chunk reads
do not deserialize, reverse payload stages, or automatically verify integrity.
`VerifyIntegrityOnRead` applies only to materialized reads, not these raw streams or
chunks. A full pass can verify SHA-256 and size; stopping early cannot verify the
complete payload. The `direct-write` example explicitly compares both against the reference.

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
workflow a streaming writer; use the activity's `UploadPayloadAsync` for direct
uploads or `StorePayloadStreamAsync` for content-addressed staging.

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
per file and 256 MiB in total so the 32 MiB stream scenarios fit; these are storage
limits, not SDK chunk sizes. Repeated runs retain data and can eventually reach the
total limit.

Production cleanup must account for readers, retries, and retained-history replays.
Use the configured payload manifest/`WorkflowPayloadCleaner` only when those references
are no longer needed; do not delete data just because the first client finished reading.
Cross-app reference calls additionally require `AllowPayloadReferences = true` and
destination access to the same stored bytes.
