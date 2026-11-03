# Dapr distributed lock client example

This console example demonstrates the modern `Dapr.DistributedLock` package and
dependency-injection registration with `AddDaprDistributedLock()`. It attempts to
acquire `myFile.txt` from the `redislock` component, holds the lock for three
seconds, and releases it automatically when the async-disposable lock handle
leaves scope.

For the older ASP.NET Core binding-driven worker demonstration, see the
[`Client\DistributedLock`](../Client/DistributedLock) example.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) with an
  [initialized local environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/).
- Redis on `localhost:6379`; the default `dapr init` environment provides it.

## Run

From the repository root:

```powershell
dapr run --app-id distributed-lock-example --resources-path .\examples\DistributedLock\components -- dotnet run --project .\examples\DistributedLock\DistributedLock --framework net10.0
```

Start the command in two terminals to observe one process acquire the lock while
the other receives no lock handle. The lock owner passed by this example is
`myApp`, and the expiration is 60 seconds.
