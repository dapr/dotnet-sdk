# Interpreted

Smart-lock device types are defined as state-machine configuration, verified before rollout, and hosted by the interpreted actor at runtime.

Tutorial: [Part 5 - Runtime-defined state machines](../../../docs/dotnet-actorsnext/tutorial/part-5.md).

The worked device type is a smart lock with `Locked`, `Unlocking`, `Unlocked`, and `Locking` states. `Unlock` moves a locked device to `Unlocking`, `MotorStopped` completes the unlock, `Lock` moves an unlocked device to `Locking`, and the next `MotorStopped` returns it to `Locked`.

The device type is an `InterpretedMachineDefinition` document. It is checked by `InterpretedMachineVerifier` before it reaches hardware, stored by `InterpretedMachineDeployer`, and then executed by the single compiled `InterpretedStateMachineActor`. Named guards and effects such as `CheckBattery` and `ActuateMotor` resolve through an `ICapabilityRegistry` of vetted compiled actions.

The control plane lists onboarded device operations through `IActorRegistry` and sends commands with `IDynamicActorClient`, so the controller does not need a compile-time smart-lock contract. This is possible because the compile-time machinery is the substrate runtime definition needs: registration, dispatch, state persistence, and weakly typed invocation all exist before the definition document arrives.

Boundary: interpreted actors carry a dynamic state bag. The typed state-migration story from Example 02 does not apply; versioning a device type means versioning its definition document as data.

The local app exposes a small HTTP API that onboards a smart-lock definition for one device id and then drives the lock through each command and sensor event in order.

Dapr 1.18 requires the sidecar to have a gRPC app channel before it accepts `SubscribeActorEventsAlpha1`. The sample configures two Kestrel endpoints in `appsettings.json`: port `5000` is the HTTP API that drives the generic device control plane, and port `5056` is the HTTP/2 app channel used by daprd.

## Start the Dapr runtime

Start the Dapr runtime locally so the app can connect to daprd over gRPC:

```powershell
dapr run --app-id actors-example-05 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5056
```

## Run Locally

Start the app from this sample directory:

```powershell
cd examples\Actor.Next\05-Interpreted
dotnet run
```

Then open `Device.Next.Example05.http` in Rider or Visual Studio and run the requests in order:

1. List runtime-onboarded device types
2. List operations for `SmartLock`
3. Onboard the `front-door` smart lock
4. Unlock: `Locked` to `Unlocking`
5. Motor stopped: `Unlocking` to `Unlocked`
6. Lock: `Unlocked` to `Locking`
7. Motor stopped: `Locking` to `Locked`

The request file uses `http://localhost:5000` and a shared `front-door` device id.

## Run Tests

Run the example tests:

```powershell
dotnet test Device.Next.Example05.Tests\Device.Next.Example05.Tests.csproj --no-restore
```

The tests use `ActorTestRuntime`; they need no sidecar, no state store, and no Docker.
