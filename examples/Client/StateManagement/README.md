# Dapr .NET SDK state management example

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

## State operations

See [StateStoreExample.cs](./StateStoreExample.cs) for an example of using `DaprClient` for basic state store operations like get, set, and delete.

# State transactions

See: [StateStoreTransactionsExample.cs](./StateStoreTransactionsExample.cs) for an example of using `DaprClient` for transactional state store operations that affect multiple keys. 

## ETags

See [StateStoreETagsExample.cs](./StateStoreETagsExample.cs) for an example of using `DaprClient` for optimistic concurrency control with the state store.
