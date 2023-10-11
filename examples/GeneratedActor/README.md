# Generated Actor Client Example

An example of generating a strongly-typed actor client.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Run the example

### Start the ActorService

Change directory to the `ActorService` folder:

```bash
cd examples/GeneratedActor/ActorService
```

To start the `ActorService`, execute the following command:

```bash
dapr run --app-id generated-service --app-port 5226 -- dotnet run
```

### Run the ActorClient

Change directory to the `ActorClient` folder:

```bash
cd examples/GeneratedActor/ActorClient
```

To run the `ActorClient`, execute the following command:

```bash
dapr run --app-id generated-client -- dotnet run
```

### Expected output

You should see the following output from the `ActorClient`:

```
== APP == Testing generated client...
== APP == Done!
```

You should see also see the following output from the `ActorService`:

```
== APP == info: GeneratedActor.RemoteActor[0]
== APP ==       GetStateAsync called.
== APP == info: GeneratedActor.RemoteActor[0]
== APP ==       SetStateAsync called.
```