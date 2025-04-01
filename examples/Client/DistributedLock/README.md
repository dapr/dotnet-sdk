# Dapr .NET SDK Distributed Lock Example

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Distributed Lock API
Dapr 1.8 introduces the Distributed Lock API. This API can be used to prevent multiple processes from accessing the same resource. In Dapr, locks are scoped to a specific App ID.

For this example, we will be running multiple instances of the same application to demonstrate an event driven consumer pattern. This example also includes a simple generator that creates some data that can be processed.

## Running the example

Navigate to the root directory of this example before performing any of the next steps.

```bash
cd examples/Client/DistributedLock
```

### Generator
In order to run the application that generates data for the workers to process, simply run the following command:

```bash
dapr run --resources-path ./Components --app-id generator -- dotnet run
```

This application will create a new file to process once every 10 seconds. The files are stored in `DistributedLock/tmp`.

### Worker
In order to properly demonstrate locking, this application will be run more than once with the same App ID. However, the applications do need different ports in order to properly receive bindings. Run them with the command below:

```bash
dapr run --resources-path ./Components --app-id worker --app-port 5000 -- dotnet run
dapr run --resources-path ./Components --app-id worker --app-port 5001 -- dotnet run
```

After running the applications, they will attempt to process files. You should see output such as:

First application:
```bash
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Received binding event on worker, scanning for work.
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Attempting to lock: 73832e04-a896-4853-9f56-f020f7f49be1
== APP == warn: DistributedLock.Controllers.BindingController[0]
== APP ==       Failed to lock 73832e04-a896-4853-9f56-f020f7f49be1.
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Received binding event on worker, scanning for work.
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Attempting to lock: f8a2d914-db83-4fe3-8ef1-af04c66da0ae
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Successfully locked file: f8a2d914-db83-4fe3-8ef1-af04c66da0ae
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Done processing f8a2d914-db83-4fe3-8ef1-af04c66da0ae
```

Second application:
```bash
lers.BindingController[0]
== APP ==       Attempting to lock: 73832e04-a896-4853-9f56-f020f7f49be1
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Successfully locked file: 73832e04-a896-4853-9f56-f020f7f49be1
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Done processing 73832e04-a896-4853-9f56-f020f7f49be1
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Received binding event on worker, scanning for work.
== APP == info: DistributedLock.Controllers.BindingController[0]
== APP ==       Attempting to lock: f8a2d914-db83-4fe3-8ef1-af04c66da0ae
== APP == warn: DistributedLock.Controllers.BindingController[0]
== APP ==       Failed to lock f8a2d914-db83-4fe3-8ef1-af04c66da0ae.
== APP == info: DistributedLock.Controllers.BindingController[0]
```

Note that both apps succeed and fail to lock files. This is due to the two apps attempting to lock the same resource at the same time.

#### Clean up
Any files that are leftover can be safely removed.

```bash
rm -rf ./tmp
```