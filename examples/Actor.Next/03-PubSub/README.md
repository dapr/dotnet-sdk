# Pub/Sub

One `[Subscribe]` attribute lets a cart react to restock events from the rest of the system.

Tutorial: [Part 3 - Dynamic pub/sub with actors](../../../docs/dotnet-actorsnext/tutorial/part-3.md).

The old SDK pattern needed a separate subscriber service plus hand-rolled routing, retry, and idempotency. Here the stream runner forwards each event through the normal actor invocation path and only acknowledges the pub/sub message after the actor turn commits.

The local app exposes a small HTTP API that marks cart items as waiting for stock, publishes an inventory restock event through Dapr pub/sub, reads cart state, checks item availability, and clears the sample carts between runs.

Dapr 1.18 requires the sidecar to have a gRPC app channel before it accepts `SubscribeActorEventsAlpha1`. The sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP API that drives the actor and publishes restock events, and port `5056` is the HTTP/2 app channel used by daprd.

## Start the Dapr runtime

Start the Dapr runtime locally so the app can connect to daprd over gRPC, open the dynamic pub/sub subscription, and publish restock events:

```powershell
cd examples\Actor.Next\03-PubSub
dapr run --app-id actors-example-03 --resources-path .\components --dapr-grpc-port 50001 --dapr-http-port 3500 --app-protocol grpc --app-port 5056
```

## Run Locally

Start the app from this sample directory:

```powershell
cd examples\Actor.Next\03-PubSub
dotnet run
```

Then open `Cart.Next.Example03.http` in Rider or Visual Studio and run the requests in order:

1. Reset demo cart
2. Reset other cart
3. Mark demo cart item unavailable
4. Mark other cart item unavailable
5. Read demo cart before restock
6. Publish restock event for demo cart
7. Read demo cart after restock
8. Check demo cart item availability
9. Check other cart item availability

The request file uses `http://localhost:5000`, a shared `demo` cart id, and an `other` cart to show that the restock event is routed only to the cart named by `RestockEvent.CartId`.

The sample includes local in-memory Dapr components under `components`: `statestore` for actor state and `orders-pubsub` for restock events. The tests use the stream runner directly, so they do not require a sidecar or pub/sub component.

## Run Tests

Run the example tests:

```powershell
dotnet test Cart.Next.Example03.Tests\Cart.Next.Example03.Tests.csproj --no-restore
```

The tests run the generated actor against `ActorTestRuntime` and verify routing, idempotent delivery, and retry behavior.
