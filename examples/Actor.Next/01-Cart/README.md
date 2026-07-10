# Cart

A cart actor you already know, with registration boilerplate, endpoint mapping, and reflection removed.

Tutorial: [Part 1 - Modern actor baseline](../../../docs/dotnet-actorsnext/tutorial/part-1.md).

This example uses `AddDaprActors()` only. There is no `MapActorsHandlers` and no per-actor registration. The actor callback stream is app-initiated: the app dials daprd, advertises its actor types, and exchanges callback frames over that outbound gRPC stream.

Dapr 1.18 requires the sidecar to have a gRPC app channel before it accepts `SubscribeActorEventsAlpha1`. The sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP API that drives the cart actor through the generated actor proxy, and port `5056` is the HTTP/2 app channel used by daprd.

## Start the Dapr runtime

Start the Dapr runtime locally by specifying the gRPC port it should listen to and provide the gRPC being listened to by the app (these ports are defined by configuration in `appsettings.json` within this project):

```powershell
dapr run --app-id actors-example-01 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5055
```

## Run Locally

Start the app from this sample directory so `appsettings.json` is loaded:

```powershell
cd examples\Actor.Next\01-Cart
dotnet run
```

Then open `Cart.Next.Example01.http` in Rider or Visual Studio and run the requests in order:

1. Add first item
2. Add second item
3. Get summary
4. Abandon cart
5. Get summary after abandon

The request file uses `http://localhost:5000` and a shared `demo` cart id.

## Run Tests

The tests run the generated actor against `ActorTestRuntime` with a fake `IPricingClient`, virtual time, and no Dapr sidecar.
