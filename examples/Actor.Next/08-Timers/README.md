# Timers

A minimal actor that exposes the whole timer API of `IActorTimerScheduler` over HTTP: schedule (create or replace),
get, list and cancel. Get and list use the `GetActorTimer` and `ListActorTimers` sidecar RPCs added in **Dapr 1.19**.

Timers are host-local: the sidecar only answers for actors it hosts, so the actor reads its own timers from
inside its turns. Calling the RPCs from an arbitrary client against another sidecar fails with `PermissionDenied`.

The sample follows [01-Cart](../01-Cart/): `AddDaprActors()` only, an app-initiated actor callback stream,
and two Kestrel endpoints in `appsettings.json` (port `5008` is the HTTP API, port `5058` the HTTP/2 app channel).

## Prerequisites

Dapr 1.19 or later is required for the get and list requests:

```powershell
dapr init --runtime-version 1.19.0-rc.6
```

## Run with Aspire

The `Timers.Next.Example08.AppHost` project starts the app and its Dapr sidecar (app id `actors-example-08`, gRPC app channel on port 5058) in one shot:

```powershell
cd examples\Actor.Next\08-Timers\Timers.Next.Example08.AppHost
dotnet run --framework net10.0
```

The Aspire dashboard shows the app and the sidecar; the HTTP API stays on `http://localhost:5008`.

## Start the Dapr runtime manually

Alternatively, start the Dapr runtime yourself, pointing it at the HTTP/2 app channel configured in `appsettings.json`:

```powershell
dapr run --app-id actors-example-08 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5058
```

Then start the app from this sample directory so `appsettings.json` is loaded:

```powershell
cd examples\Actor.Next\08-Timers
dotnet run --framework net10.0
```

## Try it

Open `http://localhost:5008` in a browser: the page has a button for each call (schedule with optional period and
TTL, list, get by name, cancel, show fired messages) and prints the HTTP response below the buttons.

Alternatively, open `Timers.Next.Example08.http` and run the requests in order: schedule a one-shot and a periodic timer,
list them, get one by name, get a missing one (404), cancel, list again, and read the messages the timers delivered.

## Run Tests

The tests run the generated actor against `ActorTestRuntime` with virtual time and no sidecar. The in-memory
timer scheduler implements get and list too, so the same code is exercised without Docker.
