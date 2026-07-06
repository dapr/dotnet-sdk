# Migration

Typed actor state migration lets actor code read the current state shape while the SDK folds older
persisted shapes forward through registered upcasters.

Tutorial: [Part 2 - Serialization and state migration](../../../docs/dotnet-actorsnext/tutorial/part-2.md).

This example is the reference usage for the migration model:

- `CartStateV1 -> CartStateV2 -> CartStateV3` uses two hand-authored upcasters. The actor reads
  `CartStateV3` with ordinary `GetOrCreateAsync` and `TryGetAsync`; it never branches on a stored
  version.
- `MyState -> MyStateV2 -> MyStateV3` is additive-only. No upcasters are written; the generator emits
  the hops.
- `RenamedState -> RenamedStateV2` is non-additive and has one authored hop.
- `GraduatedCartState` demonstrates the offramp: `GraduateAsync` writes the value in plain form so that
  entry can leave the migration envelope.

Imports use the same API as normal writes. Posting a V1 or V2 payload calls `SetAsync` with that legacy
node type; the next read folds it to the current type and the store heals on the actor turn flush.

The sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP API that
drives the migration actor through the generated actor proxy, and port `5056` is the HTTP/2 app channel
used by daprd.

## Start the Dapr runtime

Start the Dapr runtime locally so the app can connect to daprd over gRPC:

```powershell
dapr run --app-id actors-example-02 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5056
```

## Run Locally

Start the app from this sample directory:

```powershell
cd examples\Actor.Next\02-Migration
dotnet run
```

Then open `Cart.Next.Example02.http` in Rider or Visual Studio and run the requests in order. It seeds
V1 and V2 cart values, reads them as V3, exercises the additive and non-additive families, and graduates
one state entry to plain storage.

## Run Tests

Run the example tests:

```powershell
dotnet test Cart.Next.Example02.Tests\Cart.Next.Example02.Tests.csproj --no-restore
```

The tests use `ActorTestRuntime.SeedStateAsync` and migration fault hooks to cover folded reads, generated
additive hops, corruption detection, family isolation, lazy re-persist, legacy imports, graduation,
global disable, and the interpreted actor boundary.
