# Dapr state management client example

This console example demonstrates the modern `Dapr.StateManagement` package:

- Registering `DaprStateManagementClient` with dependency injection.
- Generating a typed `IWidgetStore` client from `[StateStore("statestore")]`.
- Saving, loading, conditionally updating, and deleting typed state.
- Calling bulk and transactional APIs through the direct client.

The generated `IWidgetStore` implementation fills in the `statestore` component
name, while the direct-client section supplies it explicitly.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) with an
  [initialized local environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/).
- A transactional state store named `statestore`; the default `dapr init`
  environment provides a compatible Redis component.

## Run

From the repository root:

```powershell
dapr run --app-id statemanagement-example --dapr-grpc-port 50001 -- dotnet run --project .\examples\StateManagement\StateManagementExample --framework net10.0
```

The application first exercises the generated typed store, then uses
`DaprStateManagementClient` directly for bulk save, bulk get, and transactional
delete operations.

## Using the package

The example uses project references for in-repository development. Applications
should reference the single `Dapr.StateManagement` NuGet package; it includes the
runtime, abstractions, and source generator.
