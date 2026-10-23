# Search example

This console project demonstrates the same Meilisearch document lifecycle using
both Dapr Search APIs:

- **Builder:** `new DaprSearchClientBuilder().Build()` creates a client owned by
  the caller. Each call supplies the component, index, and document type. This
  approach also supports store-wide `ListIndexesAsync`.
- **Source generator:** `[SearchIndex("searchstore", "generated-search-example-products")]`
  binds `IProductSearch : IDaprSearchIndex<Product>` at compile time.
  `services.AddDaprSearchClient().AddProductSearch()` registers its generated
  implementation. The DI provider owns the underlying client; the typed wrapper
  does not dispose it.

Each approach creates an index, indexes two products, retrieves and searches for
typed content, deletes the documents, and finally deletes the index. The example
checks acknowledgements, item failures, and returned content, and exits with an
error rather than reporting failed operations as successful.

## Reading the code

Start with `SearchExample\Program.cs`: the top-level code creates the builder
client, then registers and resolves the generated client. Compare
`RunWithBuilderAsync` and `RunWithGeneratedIndexAsync` below it; they perform the
same steps in the same order, with each SDK call separated from its result check.
The helpers at the bottom supply sample products and verify the responses.

`SearchExample\IProductSearch.cs` contains the attribute and interface that
generate `AddProductSearch()` and the typed implementation. Notice how the
generated walkthrough no longer repeats the component name, index name, or
`<Product>` type argument.

## Prerequisites

- .NET 8, 9, or 10 SDK.
- Docker with Docker Compose.
- Free local ports 3500 and 50001.

Compose starts Meilisearch and a Dapr sidecar; no local Dapr installation is
needed. The runtime must include the alpha Search API and `search.meilisearch`
component. The default is `daprio/daprd:edge`; 1.19.0 RCs through rc.4 do **not**
include Search. Set `DAPR_RUNTIME_VERSION` to select another compatible image.

## Run

From the repository root, in PowerShell:

```powershell
Set-Location examples\Search
$env:MEILI_MASTER_KEY = [Guid]::NewGuid().ToString("N")
$env:DAPR_GRPC_ENDPOINT = "http://127.0.0.1:50001"
docker compose up -d

# Wait for HTTP 204 before running the client; retry if the sidecar is still starting.
Invoke-WebRequest http://127.0.0.1:3500/v1.0/healthz/outbound

dotnet run --project .\SearchExample --framework net10.0
```

Use `net8.0` or `net9.0` as appropriate. The sidecar is local-only and does not
require a Dapr API token; unset an inherited `DAPR_API_TOKEN` when using this
Compose setup. Meilisearch is accessible only within the Compose network and
receives a random master key shared with the sidecar through an environment
variable. These settings are for local development, not production deployment.

If the default ports are already in use, set `SEARCH_GRPC_PORT` and
`SEARCH_HTTP_PORT` before starting Compose. Update `DAPR_GRPC_ENDPOINT` and the
health-check URL above to use those host ports.

Output includes a `Builder` section and a `Source generator` section, each
showing two indexed products and a match for `Dapr cookbook`, followed by
`Both Search approaches completed successfully.`

The generated index has a fixed name. Run only one instance of this example at
a time, against the isolated example service: the program deletes that index
and its contents during cleanup. The builder example uses a unique index name.

Stop and remove the example containers when finished:

```powershell
docker compose down --volumes
Remove-Item Env:\MEILI_MASTER_KEY
Remove-Item Env:\DAPR_GRPC_ENDPOINT
```

## Consistency and serialization

Writes normally return when accepted, which does not guarantee indexing has
finished. This example uses `WaitForCompletion`, a 20-second wait timeout, and
`FailRequest` so the subsequent reads can observe completed writes. Always
inspect `FailedItems` even when the acknowledgement is `Completed`.

`IncludeContent = true` (or `includeContent: true` on document reads) requests
the typed product content; otherwise content is omitted by default.
The generated API delegates to the same client and uses its JSON options.
It generates index bindings, not JSON serializers. An explicitly declared
`JsonSerializerContext` can be supplied with `UseJsonSerializationOptions`
when source-generated JSON serialization is desired.

## Using NuGet

The example references runtime and generator projects to use this repository's
SDK source. In an application outside the repository, replace those references
with the single `Dapr.Search` NuGet package from a release containing Search
(or a locally built package). It bundles the abstractions, runtime, and
generator; no separate analyzer reference is needed.
