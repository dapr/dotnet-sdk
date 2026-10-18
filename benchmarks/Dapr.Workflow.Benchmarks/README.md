# Workflow data-reference benchmark

An end-to-end SDK/sidecar benchmark, not a BenchmarkDotNet microbenchmark. Each
workflow runs **nine sequential activities** with a document whose serialized
UTF-8 JSON is exactly **2 MB (2,000,000 bytes)**. Eight activities perform small
coordination operations; only the ninth needs the document and computes a SHA-256.
Every case must return the same checksum.

The size uses decimal MB deliberately: a full 2 MiB document plus activity metadata
exceeds the scheduler's default 2 MiB message ceiling in Dapr 1.19.0-rc.3.

| Case | Workflow input | Storage and materialization |
|---|---|---|
| `Inline` | `BenchmarkDocument` | No payload registration. Full document in workflow history; deserialize at each orchestration execution. |
| `Automatic` | `BenchmarkDocument` | Data in the binary store, references in history. Automatically retrieve and deserialize for each execution and the processing activity. |
| `References` | `PayloadReference<BenchmarkDocument>` | Replay only the reference. Retrieve and deserialize once, in activity nine. |

The offloaded cases call `WithBinaryStorePayloads` on the workflow builder returned
by `AddDaprWorkflowBuilder(...)`. The inline case keeps workflow registration but
does not enable payload storage.
The automatic case selects `PayloadWritePreference.PreferReference`; the explicit-reference
case uses the default `Preserve` preference.

Both offloaded cases initially store the document through `StoreAsync` before
scheduling, so initial serialization/upload time can be reported separately from
execution. The automatic case still uses ordinary document-typed workflow/activity
inputs and automatic offloading at the activity boundary. It can attempt a duplicate
content-addressed upload at that boundary; the benchmark counts that work rather
than pretending deduplication avoids all upload traffic.

## Run

Prerequisites: .NET 10, Dapr with binary-store support (1.19, including
`1.19.0-rc.3`), and local Redis/placement/scheduler infrastructure from `dapr init`.
Record `dapr --version` with your results.

From this directory, start a **dedicated** sidecar in one terminal:

```powershell
dapr run --app-id workflow-data-reference-benchmark --dapr-http-port 3500 --dapr-grpc-port 50001 --max-body-size 32Mi --resources-path .\components
```

Do not attach other workers or schedule unrelated workflows on that app ID. The
runner switches host configurations sequentially; per-turn attribution assumes
one benchmark workflow at a time. The Redis component uses `localhost:6379`.

In another terminal, from this directory:

```powershell
dotnet run -c Release -- --Iterations=5 --WarmupRounds=1 --HistoryCache=true --Output=results-cached.json
dotnet run -c Release -- --Iterations=5 --WarmupRounds=1 --HistoryCache=false --Output=results-full-history.json
```

`HistoryCache=true` preserves the SDK default: the sidecar can send history deltas
instead of retransmitting the entire document every turn. `false` disables that
optimization equally for all three cases to measure full-history replay traffic.
**Do not generalize the full-history result to applications using cached history.**
Even with caching enabled, document-typed workflows still materialize their input
on each execution. The report records whether each incoming turn actually used
cached history, rather than assuming the runtime honored the capability.

The default gRPC endpoint is `http://localhost:50001`; set `DAPR_GRPC_ENDPOINT` for
another port. `--StoreName=...` selects another binary-store component. All workflow
client/worker channels use identical 32 MiB message limits so the baseline's larger
history is not rejected. Configure the sidecar's matching limit as shown above.

The runner warms up each case, rotates execution order between rounds, and
establishes each new worker connection using an unmeasured trivial workflow.
`--WarmupRounds=0 --Iterations=1` is useful for a smoke test, **not** a performance
claim. Failed workflows, mismatched outputs, unexpected activity/materialization
counts, and cleanup failures exit with an error rather than produce a successful report.

## Measurements

Console output gives per-sample timings and medians with min/max total time.
`Total speedup` is the inline median divided by the case's median total time:
less than 1 means slower, not an improvement.
The JSON report retains every measured sample and a `Turns` array for **each
orchestration execution**, including the initial execution. Do not assume nine
activities always means nine executions.

| Field | Meaning |
|---|---|
| `PreparationMs` | Initial document serialization/storage for offloaded cases; effectively zero for inline. |
| `ExecutionMs` | Scheduling through completion, including processing and any boundary offloading. |
| `TotalMs` | Preparation plus execution; use this for end-to-end comparisons. |
| `AllocatedBytes` | Process-wide managed allocations during the window; not peak memory, not just workflow allocations, and excludes sidecar allocations. |
| `WorkflowSentProtobufBytes` / `WorkflowReceivedProtobufBytes` | Serialized protobuf bodies for scheduling, work items, completions, and history fetches. Excludes metadata/status queries and health pings. |
| `BinaryUploadBytes` / `BinaryDownloadBytes` | File-content bytes consumed by SDK uploads / returned by downloads, including duplicate upload attempts. |
| `BinaryWrites` / `BinaryReads` | SDK file operation attempts. Retries are not presented as exactly-once processing. |
| `DocumentDeserializations` / `MaterializedDocumentUtf8Bytes` | Completed deserializations of the document, excluding reference-envelope parsing. |
| `Turns[].ReceivedProtobufBytes` | Incoming work-item body plus any separately fetched history for that turn. |
| `Turns[].DeliveredInputUtf8Bytes` | Workflow input bytes actually present in delivered history. Can be zero on a cached-history turn. |
| `Turns[].MaterializedDocumentUtf8Bytes` | Document bytes deserialized on that turn, including data reconstructed from cached history or downloaded from the binary store. |
| `Turns[].BinaryDownloadBytes` | File-content downloads during that orchestration turn; excludes activity-nine downloads. |

These are **logical message/file-content byte counts, not network packet bytes**.
They exclude gRPC/HTTP2 framing, headers, TLS, retransmissions, compression, and
sidecar-to-Redis/binary-store backend traffic. Upload bytes describe bytes consumed
by the client SDK, not necessarily bytes accepted by the store. Do not add
materialization counts to transfer counts: the same bytes may be counted in both
for different purposes.

Completion waits request no inputs or outputs. Raw result inspection happens
outside the measurement window, so metadata hydration cannot add a hidden download
to the reference case. Instrumentation is benchmark-local, uses public serializer,
client, and gRPC interfaces, and does not expand the SDK's public API.

## Interpretation and cleanup

Expect lower document materialization and replay input size with explicit
references, **not a guaranteed latency improvement on every machine**. Automatic
offloading reduces history size but adds storage reads for materialized inputs.
Cached history can eliminate repeated inline transfers without eliminating those
deserializations. The in-memory binary store minimizes backend latency; repeat with
a representative durable component before drawing production conclusions.

Input generation is seeded and uses varied, JSON-safe ASCII rather than a single
repeated character. Dataset generation, host startup/shutdown, connection warmup,
result verification, and cleanup are outside the timed window. Instrumentation
and background SDK work contribute to measured allocations and time. Protobuf size
and UTF-8 byte counting add work proportional to the data inspected, so these are
instrumented timings, not an instrumentation-free production latency prediction.

Successful runs purge only their own workflow histories, then delete their
tracked binary objects, before stopping the worker. The in-memory manifest and
store are **benchmark-only**, not a durable production cleanup design. On failure,
the printed instance ID identifies retained data; do not delete payloads while a
workflow could still use them. Stop the dedicated sidecar when finished. Restarting
the demo store loses its data, even if histories remain in Redis.
