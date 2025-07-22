# Dapr .NET SDK Bulk publish example

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Running the example

### Run the subscriber

Navigate to the [ControllerSample](https://github.com/dapr/dotnet-sdk/tree/master/examples/AspNetCore/ControllerSample) directory and run the subscriber. It will subscribe to the `deposit` topic that the publisher will publish messages to.

```sh
dapr run --app-id controller --app-port 5000 -- dotnet run
```

### Run the bulk publisher
After running the subscriber, run the bulk publisher. To run the sample locally, run this command in this project root directory:

```sh
dapr run --app-id DaprClient -- dotnet run <sample number>
```

Running the following command will output a list of the samples included:

```sh
dapr run --app-id DaprClient -- dotnet run
```

Press Ctrl+C to exit, and then run the command again and provide a sample number to run the samples.

For example run this command to run the 0th sample from the list produced earlier.

```sh
dapr run --app-id DaprClient -- dotnet run 0
```

## Publishing Bulk Pub/Sub Events

See [BulkPublishEventExample.cs](./BulkPublishEventExample.cs) for an example using the `DaprClient` to publish a pub/sub event.
