# Dapr Client Sample
The client sample shows how to make Dapr calls to publish events, save state, get state and delete state using a Dapr client apis. 

## Prerequistes

* [.Net Core SDK](https://dotnet.microsoft.com/download)
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

  ```sh
 dapr run --app-id DaprClient -- dotnet run 0
 ```

 Samples that use HTTP-based service invocation will require running the [RoutingService](../../AspNetCore/RoutingSample).
 
 Samples that use gRPC-based service invocation will require running [GrpcService](../../AspNetCore/GrpcServiceSample).

## Invoking Services

This solution contains a sample [RoutingSample service](../../AspNetCore/RoutingSample), which implements a simple banking application in ASP.NET core.

The service provides following operations:

- balance
- withdraw
- deposit

The service is a typical HTTP service.

See: [RoutingSample service](../../AspNetCore/RoutingSample/Startup.cs) for the defition of the service.

See: `InvokeServiceHttpClientExample.cs` for an example of using `HttpClient` to invoke another service through Dapr.

See: `InvokeServiceHttpExample.cs` for an example using the `DaprClient` to invoke another service throught Dapr.

 ## Working with cancellation tokens

Asynchronous APIs exposed by `DaprClient` accept a cancellation token and by default, if the operation is canceled, you will get an OperationCanceledException. However, if you choose to initialize and pass in your own GrpcChannelOptions to the client builder, then unless you enable the [ThrowOperationCanceledOnCancellation setting](https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Net.Client.GrpcChannelOptions.html#Grpc_Net_Client_GrpcChannelOptions_ThrowOperationCanceledOnCancellation), the exception thrown would be an RpcException with StatusCode as Cancelled. To get an OperationCanceledException instead, refer to the code below:-
 ```c#
            var httpClient = new HttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();
```
