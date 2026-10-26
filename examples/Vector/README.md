# Vector building block (alpha)

`Dapr.Vector` provides a standalone client and source-generated, collection-bound
clients for pre-embedded dense vectors. Embedding generation, sparse/named vectors,
hybrid fusion, and query pagination are outside this alpha API.

Install the single `Dapr.Vector` package in applications. It bundles Abstractions,
Runtime, and the source generator; the example uses project references only for
in-repository development.

## Run the example

Requires Docker and a Dapr runtime containing the Vector API and
`vector.meilisearch` component. The example defaults to `edge`. Refresh the image
if a cached edge build reports `incorrect type vector.meilisearch` or `Unimplemented`.

From this directory, in PowerShell:

```powershell
$env:MEILI_MASTER_KEY = [Guid]::NewGuid().ToString("N")
docker compose pull
docker compose up -d
$env:DAPR_GRPC_ENDPOINT = "http://localhost:50001"
$env:DAPR_HTTP_ENDPOINT = "http://localhost:3500"
dotnet run --project VectorExample --framework net10.0
docker compose down
```

The program creates `sdk-vector-example`, stores two pre-computed three-dimensional
embeddings, queries and filters them, demonstrates query-by-ID and a mixed-success
batch, then deletes its records and collection. Do not run concurrent copies against
the same store. Set `VECTOR_GRPC_PORT`/`VECTOR_HTTP_PORT` before Compose startup and
update the corresponding Dapr endpoint variables if the default ports are occupied.

## Direct and generated clients

```csharp
using Dapr.Vector;
using Dapr.Vector.Extensions;

using var client = new DaprVectorClientBuilder().Build();
await client.CreateCollectionAsync("vectorstore", "products", 3, DistanceMetric.Cosine);

// For generated clients, declare a top-level, non-generic partial interface:
[VectorCollection("vectorstore", "products")]
public partial interface IProductVectors : IDaprVectorCollection<Product>;
public sealed record Product(string Title);

// In application startup:
// services.AddDaprVectorClient().AddProductVectors();
```

Generated wrappers expose collection create/get/delete, upsert/get/delete records,
single queries and batch queries. Store-wide `ListCollectionsAsync` remains on the
direct client. Registration follows the underlying client's singleton, scoped or
transient lifetime. Wrappers do not dispose the shared client.

Use `UseJsonSerializationOptions` to configure typed payload serialization, including
a `JsonSerializerContext` for reflection-free serialization as in the example.
`VectorRecord<T>.Payload` uses JSON for all types **except `byte[]`**, which sends and
receives the proto's opaque bytes unchanged, including empty payloads. A missing
payload is `default(T)`; use nullable value types if absence must be distinguished
from zero. Record `Metadata` is a JSON object and is filterable; the payload is not.
The separate method-level metadata dictionary configures component-specific behavior.

## Semantics

- Supply positive collection dimensions and vectors of that length. The provider
  reports invalid vector values/dimensions as item failures without discarding valid
  records. No extra collection lookup is performed by the client.
- Exactly one of `VectorQuery.Vector` and `ById` is required. `TopK` omitted or zero
  selects the component default. Values and payloads are omitted from queries by
  default; get operations omit values but return stored payloads. Missing IDs are
  omitted, and found records retain request order.
- Cosine and dot-product scores are higher-is-better; Euclidean distance is
  lower-is-better. Scores are unnormalized. `ScoreThreshold` is inclusive in the
  corresponding direction. Results report the effective metric.
- Meilisearch supports cosine only. Its query-by-ID uses similar-document search,
  which excludes the source record. Explicit-vector queries may return that record.
- Batch queries return one `Response` or `Error` per query in order. Errors retain
  canonical gRPC status codes and protobuf Any details. Request-wide failures throw
  `DaprException` with the underlying `RpcException`; caller cancellation propagates.
- Writes without options return on acceptance, not completion. `Queued` with no
  failures is not a guarantee of eventual success. For completed writes, specify
  `WaitForCompletion`, a positive `WaitTimeout`, and an explicit timeout action.
  Keep the wait timeout below runtime resiliency/RPC deadlines as described by
  `IndexingOptions`. Inspect both the acknowledgement and per-item failures.
- JSON metadata uses protobuf Struct numbers (doubles); store integers beyond
  exact double precision as strings.

## Tests

From the repository root:

```powershell
dotnet test test\Dapr.Vector.Test\Dapr.Vector.Test.csproj
docker pull daprio/daprd:edge
$env:DAPR_RUNTIME_VERSION = "edge"
dotnet test test\Dapr.IntegrationTest.Vector\Dapr.IntegrationTest.Vector.csproj --framework net10.0
```

Integration tests use isolated Meilisearch and Dapr sidecar containers through
`DaprHarnessBuilder.BuildVector()`. They cover direct and generated clients, binary
payloads, filtering, scoring, inclusion flags, per-query and per-item failures,
collection lifecycle, and queued writes.
