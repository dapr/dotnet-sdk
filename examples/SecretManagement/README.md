# Dapr Secrets Management Sample

This sample demonstrates how to use the Dapr Secrets Management SDK to retrieve secrets from Dapr secret store components.

## Features Demonstrated

1. **Direct secret retrieval** — Using `DaprSecretsManagementClient` to fetch individual or bulk secrets via gRPC.
2. **Typed secret stores** — Using the `[SecretStore]` and `[Secret]` attributes with the source generator to create strongly-typed secret accessors.
3. **Dependency injection** — Registering the secrets client and typed stores via `IServiceCollection` extensions.

## Prerequisites

- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A configured Dapr secret store component (e.g., local file, Kubernetes secrets, Azure Key Vault)

## Running the Sample

```bash
dapr run --app-id secret-sample --app-port 5234 -- dotnet run
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/secrets/{storeName}/{key}` | Retrieve a single secret by key |
| GET | `/secrets/{storeName}` | Retrieve all secrets from a store |

## NuGet Package Note

When consuming from NuGet, install the single **`Dapr.SecretsManagement`** package. The sub-projects (`Abstractions`, `Runtime`, `Generators`) are bundled into this one package and are not published individually.

```xml
<PackageReference Include="Dapr.SecretsManagement" Version="<version>" />
```
