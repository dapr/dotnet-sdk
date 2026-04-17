# Dapr VirtualActors Examples

These examples demonstrate the new `Dapr.VirtualActors` framework — a DI-first, AOT-safe
replacement for the classic `Dapr.Actors` package.

## Key differences from classic Dapr.Actors

| Feature | Dapr.Actors (classic) | Dapr.VirtualActors (new) |
|---|---|---|
| Registration | `ActorRuntime.RegisterActor<T>()` (static) | `services.AddDaprVirtualActors()` (DI-first) |
| ASP.NET Core | Required (`app.MapActorsHandlers()`) | Not required (generic host) |
| Transport | HTTP callbacks from Dapr sidecar | Persistent gRPC connection |
| AOT safety | Reflection-based | Reflection-free (source generators) |
| Auto-registration | Manual | Source generator auto-discovers actors |

## Examples

### BasicActor — Hello World

`BasicActor/` demonstrates the minimal setup for a VirtualActor:

- `Interfaces/` — actor interface project (no runtime dependency)
- `Service/` — actor implementation hosted as a generic .NET host
- `Client/` — proxy-based client calling the actor

```bash
# Start the service (requires Dapr sidecar)
dapr run --app-id basic-actor --app-port 5001 -- dotnet run --project BasicActor/Service

# Run the client
dapr run -- dotnet run --project BasicActor/Client
```

### ReentrancyActor — Actor-to-actor calls

`ReentrancyActor/` demonstrates reentrancy configuration, required when an actor calls itself
or another actor in a chain that circles back. Without reentrancy, such patterns deadlock.

```csharp
services.AddDaprVirtualActors(options =>
{
    options.Reentrancy.Enabled = true;
    options.Reentrancy.MaxStackDepth = 32;
});
```

### NamespacingActor — Multi-tenant namespacing

`NamespacingActor/` shows how to register the same actor implementation under multiple
type names — one per tenant — for logical isolation without separate Dapr components.

```csharp
foreach (var tenantId in tenantIds)
{
    options.RegisterActor<TenantAccountActor>(
        factory: h => new TenantAccountActor(h),
        actorTypeName: $"Tenant_{tenantId}_AccountActor");
}
```
