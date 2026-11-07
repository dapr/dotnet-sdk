# Dapr secrets management example

This sample demonstrates the modern `Dapr.SecretsManagement` package:

1. Direct retrieval with `DaprSecretsManagementClient`.
2. Bulk retrieval from a named secret store.
3. A source-generated typed store declared with `[SecretStore]` and `[Secret]`.
4. Dependency-injection registration through `AddDaprSecretsManagementClient()`.

`IMyVaultSecrets` binds to the `my-vault` component and loads
`db-connection-string` and `ApiKey`. The checked-in local-file component and
values are development-only data that make the example runnable without an
external vault.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) with an
  [initialized local environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/).

## Run

From the repository root:

```powershell
Set-Location examples\SecretManagement
dapr run --app-id secret-sample --app-port 6543 --resources-path .\components -- dotnet run --project .\SecretManagementSample --framework net10.0
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/secrets/{storeName}/{key}` | Retrieve one key through `DaprSecretsManagementClient`. |
| `GET` | `/secrets/{storeName}` | Retrieve all secrets from a store. |
| `GET` | `/typed-secrets` | Return `DatabaseConnection` and `ApiKey` through the generated `IMyVaultSecrets` implementation. |

For example:

```powershell
Invoke-RestMethod http://localhost:6543/typed-secrets
Invoke-RestMethod http://localhost:6543/secrets/my-vault/ApiKey
```

## Files

- [`IMyVaultSecrets.cs`](./SecretManagementSample/IMyVaultSecrets.cs) declares
  the generated typed client.
- [`my-vault.yaml`](./components/my-vault.yaml) configures the local-file store.
- [`secrets.json`](./secrets.json) supplies non-sensitive demonstration values.

## Using the package

Applications should install the single `Dapr.SecretsManagement` package. It
includes the abstractions, runtime, and source generator; those sub-projects are
not published as separate packages.
