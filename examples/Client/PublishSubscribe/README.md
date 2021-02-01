# Dapr .NET SDK pub/sub example

## Prerequisites

- [.NET Core SDK](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Running the example

To run the sample locally run this command in the DaprClient directory:

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

## Publishing Pub/Sub Events

See [PublishEventExample.cs](./PublishEventExample.cs) for an example using the `DaprClient` to publish a pub/sub event.

