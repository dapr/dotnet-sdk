# Example - Distributed Lock APIs

This example demonstrates the Distributed Lock APIs in Dapr.
It demonstrates the following APIs:
- **Distributed Lock**: Try Lock
- **Distributed Lock**: Unlock

> **Note:** Make sure to use the latest proto bindings

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Overview
This example shows the usage of two different Distributed Lock APIs. The TryLock call and
the Unlock call. Both of these calls can be handled in two different ways.

#### TryLock Example
```csharp
var tryLockResponse = await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds);
```

#### Unlock Example
```csharp
var unlockResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
```

### Start the DistributedLock TryLockApplication.

Change directory to this folder:

```bash
cd examples/Client/DistributedLockApi/TryLockApplication
dotnet build
```

To run the `TryLockApplication`, execute the following command:

```bash
dapr run  --app-id distributedLock --app-protocol grpc --components-path ./Components --log-level debug dotnet run
```

You should see the following output from the application:

```
== APP == Getting deposited value: 200
== APP == Unlock API response: Success
== APP == Unlock API response when lock is not acquired: LockUnexist
== APP == Acquired Lock? True
```

### Start the DistributedLock UnLockApplication.

Run `UnLockApplication` after `TryLockApplication` is ran.

Change directory to this folder:

```bash
cd examples/Client/DistributedLockApi/UnLockApplication
dotnet build
```

To run the `UnLockApplication`, execute the following command:

```bash
dapr run  --app-id distributedLock --app-protocol grpc --components-path ./Components --log-level debug dotnet run
```

You should see the following output from the application:

```
== APP == Unlock API response when lock is acquired by a different process: LockBelongToOthers
```