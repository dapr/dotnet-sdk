# Dapr Client examples

The following examples will show you how to:

- Invoke services
- Publish events
- Use the state store to get, set, and delete data

## Prerequisites

* [.Net Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)

## Running the Sample

To run the sample locally run this command in DaprClient directory:
```sh
dapr run --app-id DaprClient -- dotnet run <sample number>
```

Running the following command will output a list of the samples included. 
```sh
dapr run --app-id DaprClient -- dotnet run
```

Press Ctrl+C to exit, and then run the command again and provide a sample number to run the samples.

For example run this command to run the 0th sample from the list produced earlier.
```sh
dapr run --app-id DaprClient -- dotnet run 0
```

Samples that use HTTP-based service invocation will require running the [RoutingService](../../AspNetCore/RoutingSample).

Samples that use gRPC-based service invocation will require running [GrpcService](../../AspNetCore/GrpcServiceSample).

## Invoking Services

See: `InvokeServiceHttpClientExample.cs` for an example of using `HttpClient` to invoke another service through Dapr.

See: `InvokeServiceHttpExample.cs` for an example using the `DaprClient` to invoke another service through Dapr.

See: `InvokeServiceGrpcExample.cs` for an example using the `DaprClient` to invoke a service using gRPC through Dapr.

## Publishing Pub/Sub Events

See: `PublishEventExample.cs` for an example using the `DaprClient` to publish a pub/sub event.

## Working with the State Store

See: `StateStoreExample.cs` for an example of using `DaprClient` for basic state store operations like get, set, and delete.

See: `StateStoreETagsExample.cs` for an example of using `DaprClient` for optimistic concurrency control with the state store.

See: `StateStoreTransactionsExample.cs` for an example of using `DaprClient` for transactional state store operations that affect multiple keys. 
