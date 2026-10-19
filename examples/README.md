# Dapr .NET Core examples

This repository contains a samples that highlight the Dapr .NET SDK capabilities:

| Sample                           | Description                                                                                                                                                                                    |
|----------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [1. Client](./Client) | The client example shows how to make Dapr calls to publish events, save state, get state and delete state using a Dapr client.                                                   
| [2. Actor](./Actor)              | Demonstrates creating virtual actors that encapsulate code and state.                                                                                                  |
| [3. ASP.NET Core](./AspNetCore)  | Demonstrates ASP.NET Core integration with Dapr by creating Controllers and Routes. |
| [4. Workflow](./Workflow)        | Demonstrates creating durable, long-running Dapr workflows using code. |
| [5. Cryptography](./Cryptography) | Demonstrates encryption and decryption operations using Dapr. |
| [6. Binary Store](./BinaryStore) | Demonstrates typed and direct binary store clients, streaming uploads and downloads, and an in-memory Dapr component. |

## Target frameworks

All example projects share the SDK's target frameworks: .NET 8, .NET 9, and .NET 10.
Set `DaprEnableNet11=true` to additionally target the .NET 11 preview; this requires
a .NET 11 SDK. The repository's `global.json` pins .NET 10, so select a .NET 11
SDK there before using the preview commands (CI removes the pin for preview jobs).
Build or run an individual example with an explicit framework:

```powershell
dotnet build examples\Client\ConfigurationApi\ConfigurationApi.csproj --framework net8.0
dotnet run --project examples\Workflow\WorkflowConsoleApp --framework net10.0
dotnet build examples\Client\ConfigurationApi\ConfigurationApi.csproj --framework net11.0 -p:DaprEnableNet11=true
```

CI builds every application example, including the Aspire hosting examples, for
the framework selected by each integration-test job. The build-validation test
uses its own target framework and build configuration, rather than always building
the examples for .NET 10. Example test projects also run across the framework matrix.

To run build validation locally for the stable frameworks:

```powershell
dotnet test test\Dapr.IntegrationTest.Examples\Dapr.IntegrationTest.Examples.csproj --configuration Release
```

For .NET 11, use a preview SDK and set the opt-in flag in the environment so that
the child example builds inherit it:

```powershell
$env:DaprEnableNet11 = 'true'
dotnet test test\Dapr.IntegrationTest.Examples\Dapr.IntegrationTest.Examples.csproj --configuration Release --framework net11.0
```