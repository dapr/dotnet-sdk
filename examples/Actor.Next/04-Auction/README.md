# Auction

A real soft-close race condition is written as a deterministic unit test, with a structural check that catches stranded states at build time.

Tutorial: [Part 4 - State machines and deterministic race testing](../../../docs/dotnet-actorsnext/tutorial/part-4.md).

The actor owns the continuously reactive auction state. The entry action for `Sold` stands in for starting a fulfillment workflow; the workflow would own the finite, multi-step fulfillment process after the actor has made the sale decision.

Dapr 1.18 requires the sidecar to have a gRPC app channel before it accepts `SubscribeActorEventsAlpha1`. The sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP API that drives the auction actor through the generated actor proxy, and port `5056` is the HTTP/2 app channel used by daprd.

## Start the Dapr runtime

Start the Dapr runtime locally so the app can connect to daprd over gRPC:

```powershell
dapr run --app-id actors-example-04 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5056
```

## Run Locally

Start the app from this sample directory:

```powershell
cd examples\Actor.Next\04-Auction
dotnet run
```

Then open `Auction.Next.Example04.http` in Rider or Visual Studio and run the requests in order:

1. Place opening bid
2. Read open auction
3. Reject lower bid
4. Accept higher bid and reschedule soft close timer
5. Close auction as sold
6. Read sold auction with fulfillment started
7. Reject bid after close
8. Expire a separate auction without a sale
9. Read expired auction

The request file uses `http://localhost:5000`, a shared `auction-demo` auction id for the sale path, and `auction-expired-demo` for the expiry path.

The deterministic soft-close race is intentionally covered by the tests, where virtual time and scheduler ordering can be controlled.

## Run Tests

Run the example tests:

```powershell
dotnet test Auction.Next.Example04.Tests\Auction.Next.Example04.Tests.csproj --no-restore
```

The tests run the generated actor against `ActorTestRuntime`, advance virtual time, and verify the state-machine structural checks.
