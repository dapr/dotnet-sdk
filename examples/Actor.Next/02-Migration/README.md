# Migration

Typed upcasters turn "old data will not deserialize after a deploy" from a production incident into a build-time error and a unit-testable migration.

Tutorial: [Part 2 - Serialization and state migration](../../../docs/dotnet-actorsnext/tutorial/part-2.md).

The example keeps the cart state deliberately small: V1 is a flat SKU list, V2 is line items with quantities, and V3 adds a derived total. The tests seed old envelopes into the in-memory runtime store and prove the actor folds the upcaster chain in one activation. The `DAPR1410` analyzer is the companion guard for catching incompatible state-shape changes during builds.

The local app exposes a small HTTP API that imports V1 or V2 state through the actor, immediately reads it back as V3, and lets you clear the sample cart between runs.

Dapr 1.18 requires the sidecar to have a gRPC app channel before it accepts `SubscribeActorEventsAlpha1`. The sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP API that drives the migration actor through the generated actor proxy, and port `5056` is the HTTP/2 app channel used by daprd.

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

Then open `Cart.Next.Example02.http` in Rider or Visual Studio and run the requests in order:

1. Import V1 state and migrate to V3
2. Read migrated state
3. Clear state
4. Import V2 state and migrate to V3
5. Read migrated V2 state

The request file uses `http://localhost:5000` and a shared `demo` cart id.

## Run Tests

Run the example tests:

```powershell
dotnet test Cart.Next.Example02.Tests\Cart.Next.Example02.Tests.csproj --no-restore
```

The tests run the generated actor against `ActorTestRuntime`, seed V1 state into the in-memory store, and verify the V1-to-V2-to-V3 upcaster chain.
