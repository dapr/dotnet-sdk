# Per-type options

Two actor types with different lifecycle profiles, configured per type from a single `AddDaprActors` call.

The app hosts:

- `CheckoutSession` - ephemeral sessions that should be deactivated quickly once idle. Registered with a per-type `IdleTimeout` of 5 minutes and `DisableStateMigration = true`, so their short-lived state is stored plainly without state-migration envelopes.
- `Inventory` - hot aggregates that should stay resident and accept reentrant call chains. Registered with a per-type `IdleTimeout` of 2 hours, `EnableReentrancy = true`, `MaxReentrantDepth = 4`, and a `DrainRebalancedActorsTimeout` of 5 seconds.

The configuration is the centerpiece of the sample and lives at the top of `Program.cs`:

```csharp
options.ActorIdleTimeout = TimeSpan.FromMinutes(30);          // app-wide default

options.Actors.RegisterActor<CheckoutSessionActor>(o =>       // per-type overrides
{
    o.IdleTimeout = TimeSpan.FromMinutes(5);
    o.DisableStateMigration = true;
});

options.Actors.RegisterActor<InventoryActor>(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(2);
    o.EnableReentrancy = true;
    o.MaxReentrantDepth = 4;
    o.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(5);
});
```

Only the fields set per type override the app-wide values; everything else is inherited. Each actor type is advertised to daprd on its own callback stream with its merged configuration, so daprd applies the 5-minute idle window to sessions and the 2-hour window to inventory. `DisableStateMigration` is the exception: it is applied locally by the runtime when persisting state, not advertised to daprd.

## Run with Aspire

The `Store.Next.Example07.AppHost` project starts the app and its Dapr sidecar (app id `actors-example-07`, gRPC app channel on port 5057) in one shot. It requires an initialized Dapr environment (`dapr init`):

```powershell
cd examples\Actor.Next\07-PerTypeOptions\Store.Next.Example07.AppHost
dotnet run
```

The Aspire dashboard shows the app and the sidecar; the HTTP API stays on `http://localhost:5007`.

## Start the Dapr runtime manually

Alternatively, start the Dapr runtime yourself, pointing it at the HTTP/2 app channel configured in `appsettings.json`:

```powershell
dapr run --app-id actors-example-07 --dapr-grpc-port 50001 --app-protocol grpc --app-port 5057
```

## Run Locally

Start the app from this sample directory so `appsettings.json` is loaded:

```powershell
cd examples\Actor.Next\07-PerTypeOptions
dotnet run
```

Opening `http://localhost:5007/` in a browser shows a summary page with the effective configuration of each actor type (which fields are overridden and which are inherited) and the available endpoints.

Then open `Store.Next.Example07.http` in Rider or Visual Studio and run the requests in order:

1. Add an item to a checkout session
2. Read the session summary
3. Restock a SKU
4. Read stock on hand

The request file uses `http://localhost:5007`.

## Run Tests

The tests exercise both generated actors against `ActorTestRuntime` with no Dapr sidecar, and assert that the per-type overrides (and only those) reach each actor type's runtime registration while unset fields inherit the app-wide defaults.
