# Dapr .NET SDK state management example

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
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

## Running with or without a Dapr API token

Two [Dapr multi-app run files](https://docs.dapr.io/developing-applications/local-development/multi-app-dapr-run/multi-app-overview/) start the bulk state sample together with its sidecar, so you can exercise `DaprClient` against a sidecar that does or does not enforce [API token authentication](https://docs.dapr.io/operations/security/api-token/):

- [dapr-no-token.yaml](./dapr-no-token.yaml): the sidecar accepts every call.
- [dapr-with-token.yaml](./dapr-with-token.yaml): sets `DAPR_API_TOKEN`, which the Dapr CLI applies to both the sidecar and the app. The sidecar then rejects every call that does not carry the `dapr-api-token` header, while `DaprClientBuilder` picks the token up from the environment and adds the header automatically.

```sh
dapr run -f dapr-no-token.yaml
dapr run -f dapr-with-token.yaml
```

With the token enabled, the sidecar logs that API token authentication is on, and a `DaprClient` method that does not forward the header fails with an `Unauthenticated` gRPC status instead of completing (see [dapr/dotnet-sdk#1282](https://github.com/dapr/dotnet-sdk/issues/1282)). Edit the last element of `command` in the run file to run a different sample.

## State operations

See [StateStoreExample.cs](./StateStoreExample.cs) for an example of using `DaprClient` for basic state store operations like get, set, and delete.

## State transactions

See: [StateStoreTransactionsExample.cs](./StateStoreTransactionsExample.cs) for an example of using `DaprClient` for transactional state store operations that affect multiple keys. 

## ETags
See [StateStoreETagsExample.cs](./StateStoreETagsExample.cs) for an example of using `DaprClient` for optimistic concurrency control with the state store.

## Bulk State
See [BulkStateExample.cs](./BulkStateExample.cs) for an example of using `DaprClient` for Bulk State, i.e. sending multiple key value pairs to the state store.
