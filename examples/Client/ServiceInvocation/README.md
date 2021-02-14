# Dapr .NET SDK service invocation example

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
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

## HTTP client

Make sure to first run the [Routing Service](../../AspNetCore/RoutingSample) to have a service to invoke.

See [InvokeServiceHttpClientExample.cs](./InvokeServiceHttpClientExample.cs) for an example of using `HttpClient` to invoke another service through Dapr.

## Dapr HTTP client
Make sure to first run the [Routing Service](../../AspNetCore/RoutingSample) to have a service to invoke.

See [InvokeServiceHttpExample.cs](./InvokeServiceHttpExample.cs) for an example using the `DaprClient` to invoke another service through Dapr.

## Dapr gRPC client

Make sure to first run the [GrpcService](../../AspNetCore/GrpcServiceSample) to have a service to invoke.

See [InvokeServiceGrpcExample.cs](./InvokeServiceGrpcExample.cs) for an example using the `DaprClient` to invoke a service using gRPC through Dapr.
