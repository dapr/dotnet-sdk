# Deterministic actor concurrency tests

`ActorTestRuntime` runs application actor factories, dispatchers, state persistence,
and the Core timer/reminder adapters without a sidecar. Supply your application's
registrations as `configureServices` (`Action<IServiceCollection>`), then control
mailbox execution with `StepAsync` and `RunToIdle`.

## Isolated runtime instances

`CreateActor<TActor>` resolves proxies from that runtime's `IActorProxyFactory`.
Overlapping runtimes keep separate invocation clients, schedulers, faults, and
state stores; constructing or disposing one does not configure or reset the
process-wide `ActorProxy` facade. Use `CreateActor` in tests and inject
`IActorProxyFactory` into actors that call other actors. Register generated actor
services before creating typed proxies; without a factory, weakly typed
`InvokeAsync` remains available but `CreateActor` throws.

Failed or cancelled activation is disposed and retried on the next invocation.
This differs from ordinary turn failure: dirty state from a failed turn remains
in the activation for a subsequent successful save.

## Exercise remote reentrancy

```csharp
await using var runtime = new ActorTestRuntime(
    configureServices,
    new ActorTestRuntimeOptions { SimulateRemoteCalls = true });

var call = runtime.InvokeAsync("Cart", ActorId.Create("one"), "Checkout");
await runtime.RunToIdle(cancellationToken);
await call;
```

`SimulateRemoteCalls` removes the ambient `ExecutionContext` from actor-to-actor
invocations while preserving the propagated `Dapr-Reentrancy-Id` header. This
exercises self-calls and A-to-B-to-A callbacks without relying on `AsyncLocal`
state surviving a transport boundary. For a single matching invocation instead:

```csharp
runtime.Faults.SimulateNextRemoteInvocation(
    actorType: "Inventory", methodName: "Reserve");
```

Unmatched calls do not consume the fault. This is a transport-boundary simulation,
not a replacement for testing placement, authentication, serialization, or routing
with a real Dapr sidecar.

Reentrancy is admitted only for an active, matching call chain and is subject to
the configured maximum depth. Explicit `EnableReentrancy = false` is enforced;
type-specific settings override application settings. When the option is unset,
the in-process runtime preserves its historical local-reentrancy behavior. Set
the option explicitly when comparing in-memory and sidecar behavior: Dapr also
enforces its own advertised actor configuration.

Await nested actor calls. Reentrancy allows a matching call chain to enter an
occupied actor; it does not make actor fields or cached state safe for arbitrary
parallel branches or fire-and-forget application work.

## Pause persistence and race deactivation

State-write pauses are one-shot, match the state value type and optional name,
and stop immediately before writing to the store:

```csharp
using var pause = runtime.Faults.PauseNextStateWrite<int>("count");
var call = runtime.InvokeAsync("Counter", ActorId.Create("one"), "Increment");
await runtime.StepAsync(cancellationToken);
await pause.Reached.WaitAsync(cancellationToken);

var deactivate = runtime.DeactivateAsync("Counter", ActorId.Create("one"));
try
{
    Assert.False(deactivate.IsCompleted);
}
finally
{
    pause.Release();
}

await runtime.RunToIdle(cancellationToken);
await Task.WhenAll(call, deactivate);
Assert.Equal(1, runtime.StateOf("Counter", ActorId.Create("one")).Get<int>("count"));
```

External deactivation is queued behind active work. A deactivation requested by
the active call chain is deferred until that turn and its admitted children
finish. With a controlled scheduler, advance the scheduler before awaiting an
external deactivation. Disposing a pause releases it, and a paused write honors
its own cancellation token. Cancelling `RunToIdle` only cancels the scheduler
wait; release the pause to let an already-running turn finish.

## Advance timers and reminders

The harness injects the production **in-process** timer/reminder adapters backed
by `runtime.Time`. Advance virtual time to admit due callbacks, then run the
controlled scheduler:

```csharp
runtime.Time.Advance(TimeSpan.FromSeconds(1));
await runtime.RunToIdle(cancellationToken);
```

Callbacks are independent mailbox turns, not reentrant continuations of their
registering turn. Advance time while a turn is paused to test that callbacks
wait for that actor but other actors can progress. Register the same timer or
reminder name concurrently, then cancel it and advance time to test replacement
ownership. Cancellation prevents future callback admission; an already-admitted
callback may still complete.

The virtual provider implements `TimeProvider.CreateTimer`, including timer
change/disposal and execution-context capture. Core adapters suppress capture.
Advance calls must not overlap or recurse. The direct `ScheduleTimer` and
`ScheduleReminder` helpers remain available. Virtual time does not control a real
sidecar's clock or reproduce durable scheduler delivery.

## Inject callback-stream failures

`FaultInjectingActorEventsTransport` decorates either a fake transport or the
real `DaprActorEventsTransport`. In your integration-test host, register the
decorator after actor services:

```csharp
services.AddSingleton<ActorFaults>();
services.AddSingleton<IActorStateFaultInjector>(
    sp => sp.GetRequiredService<ActorFaults>());
services.AddSingleton<ISubscribeActorEventsTransport>(sp =>
    new FaultInjectingActorEventsTransport(
        createTransport(sp),
        sp.GetRequiredService<ActorFaults>()));
```

Here `createTransport` is your existing transport factory, preserving its
endpoint and API-token configuration. Do not resolve
`ISubscribeActorEventsTransport` inside that factory, which would recursively
resolve the decorator. The state-fault registration is only needed when using
state-write/migration faults in that host.

```csharp
faults.FailNextStreamRead(SubscribeActorEventsFrameKind.Invoke);
// Or, in a separate scenario:
faults.FailNextStreamWrite(SubscribeActorEventsFrameKind.Invoke);
```

A read fault drops the selected incoming frame before dispatch. A write fault
drops the selected response before forwarding it; a successful actor turn has
already committed state at that point. Both are one-shot and leave nonmatching
frames untouched. Assert reconnection and subsequent progress, allowing the
configured reconnect delay. Test retries with idempotent operations: response
loss does **not** imply that state was rolled back, and delivery is at least once.

These controls inject transport failures and deterministic interleavings rather
than reintroducing broken SDK behavior. The stream reader continues admitting
callbacks while other actor calls are suspended, avoiding nested-call worker
exhaustion. Outstanding work consumes memory proportional to the admitted load;
application code must still honor cancellation during shutdown.

Permanent examples of these checks live in `RuntimeConcurrencyTests`,
`TimerConcurrencyTests`, `StreamConcurrencyTests`, `ActorStreamFaultTests`,
`VirtualTimerConcurrencyTests`, and `RealSidecarConcurrencyTests`. The sidecar
tests use Dapr 1.18 with placement, scheduler, and Redis. Timer ownership and
execution-context regressions belong to the SDK's in-process adapters; the
sidecar tests separately verify Dapr callback serialization and state durability.
