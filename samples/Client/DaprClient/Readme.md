# Dapr Client Sample
The client sample shows how to make Dapr calls to publish events, save state, get state and delete state using a Dapr client apis. 

## Prerequistes
* [.Net Core SDK 3.1](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)


 ## Running the Sample

 To run the sample locally run this command in DaprClient directory:
 ```sh
 dapr run --app-id DaprClient -- dotnet run
 ```

 Above command will run Dapr runtime and launch the app and will show logs both form Dapr runtime and the application. The client app will make calls to Dapr runtime to publish events, save state, get state and delete state using the gRPC client.
 Logs from application will show following in the command window:
```sh
Published Event!
Saved State!
Got State: small  yellow
Deleted State!
Executing transaction - save state and delete state
Executed State Transaction!
State not found in store
Published deposit event!
Done
 ```

To invoke RoutingService via HTTP, run [RoutingService](../../AspNetCore/RoutingSample) or to invoke GrpcService via gRPC, run [GrpcService](../../AspNetCore/GrpcServiceSample) sample first. Then, invoke the client using the corresponding command below:-

``` sh
 dapr run --app-id DaprClient -- dotnet run --useRouting true
```

or

``` sh
 dapr run --app-id DaprClient -- dotnet run --useGrpcsample true
```

or to invoke RoutingService via HTTP and GrpcService via gRPC simultaneously:

``` sh
 dapr run --app-id DaprClient -- dotnet run --useRouting true --useGrpcsample true
```

## Invoking Services
This solution contains a sample [RoutingSample service](../../AspNetCore/RoutingSample), which implements a simple banking application in ASP.NET core.
The service provides following operations:
- balance
- withdraw
- deposit

The service is a typical HTTP service. The following sample demonstrates how to use the Dapr .NET SDK to invoke a service listening on HTTP.

Operation *balance* is a GET-operation mapped as (see startup.cs in the *RoutingService*):
 ```c#
 endpoints.MapGet("{id}", Balance);
 ```

It can be invoked via HTTP by using following urls.
On Windows, Linux, MacOS:
 ```sh
curl -X GET http://127.0.0.1:5000/17
 ```

Dapr supports out-of-the-box invocation of the service operation.
To demonstrate this, take a look at the example inside `InvokeBalanceServiceOperationAsync()`.

The following example invokes another service:

```c#

var res = await client.InvokeMethodAsync<object>(serviceName, methodName);
```

In this example, the HttpGet method is invoked on the service with name *serviceName*. This argument specifies the name of the service, which was used when in conjunction with *dapr run –app-id serviceName*.The *route* argument specifies the route of the service endpoint.
For example, if your service listens on HTTP, the route is typically defined by attribute *HttpPost(“route”)* or *HttpGet(“route”)*. Last argument is called metadata and it specifies GET operation in its example.


Second service operation *withdraw* can be invoked by HTTP POST request, but also triggered as a *cloud event* by publishing the event to the topic with name 'withdraw'.
To enable this, the operation is mapped as:
 ```c#
endpoints.MapPost("withdraw", Withdraw).WithTopic("withdraw");
 ```
and it can also be invoked (triggered) by following url:

On Linux and MacOS:
 ```sh
curl -X POST http://127.0.0.1:5000/withdraw \
        -H 'Content-Type: application/json' \
        -d '{ "id": "17", "amount": 10 }'
 ```
On Windows:
 ```sh
curl -X POST http://127.0.0.1:5000/withdraw -H "Content-Type: application/json" -d "{ \"id\": \"17\", \"amount\": 1 }"
 ```


The method *InvokeWithdrawServiceOperationAsync* demonstrates how to use DAPR .NET SDK to invoke a POST operation on another service.

 ```c#        
            ...

            var data = new { id = "17", amount = (decimal)10, };
            
            // The HttpInvocationOptions object is needed to specify additional information such as the HTTP method and an optional query string, because the receiving service is listening on HTTP.  If it were listening on gRPC, it is not needed.
            var httpOptions = new HttpInvocationOptions()
            {
                Method = HttpMethod.Post
            };

            await client.InvokeMethodAsync<object>("routing", "Withdraw", data, httpOptions);
 ```

Because, the same operation subscribes events on the *withdraw* topic, it can be invoked by event:
``` 
dapr publish -t withdraw -p '{"id": "17", "amount": 15 }'
``` 

Operation *deposit* can be invoked by HTTP POST request and also triggered as a *cloud event* by publishing the event to the topic with name 'deposit'.
It is mapped as:
 ```c#
endpoints.MapPost("deposit", Withdraw).WithTopic("deposit");
 ```
You can also use a **dapr** cli to publish the event to invoke te operation *deposit*: 
``` 
dapr publish -t deposit -p '{"id": "17", "deposit": 15 }'
 ``` 

The method *PublishDepositeEventToRoutingSampleAsync* demonstrates how to publish an event to the dapr runtime, which triggers the operation 'deposit'.
 ```c#
            await client.PublishEventAsync("deposit", new  { id = "17", amount = (decimal)10, });          
 ```


## Handling RpcException

Run the controller sample as follows from samples/AspNetCore/ControllerSample directory:
```
dapr run --app-id controller --app-port 5000 dotnet run
```

Run the client sample as follows from samples/Client/DaprClient directory. Setting the "--rpc-exception" argument to true will invoke a route on the server side that causes it to throw an RpcException:
```
dapr run --app-id DaprClient dotnet run --rpc-exception true
```

The controller sample has a route "/throwException" that returns a BadRequest result which causes the Dapr sidecar to throw an RpcException. The method *InvokeThrowExceptionOperationAsync* on the client side demonstrates how to extract the error message from RpcException.
```c#
            var entry = ex.Trailers.Get(grpcStatusDetails);
            var status = Google.Rpc.Status.Parser.ParseFrom(entry.ValueBytes);
            Console.WriteLine("Grpc Exception Message: " + status.Message);
            Console.WriteLine("Grpc Statuscode: " + status.Code);
            foreach(var detail in status.Details)
            {
                if(Google.Protobuf.WellKnownTypes.Any.GetTypeName(detail.TypeUrl) == grpcErrorInfoDetail)
                {
                    var rpcError = detail.Unpack<Google.Rpc.ErrorInfo>();
                    Console.WriteLine("Grpc Exception: Http Error Message: " + rpcError.Metadata[daprErrorInfoHTTPErrorMetadata]);
                    Console.WriteLine("Grpc Exception: Http Status Code: " + rpcError.Metadata[daprErrorInfoHTTPCodeMetadata]);
                }
            }
 ```

 ## Working with cancellation tokens

 InvokeMethodAsync and other APIs exposed by Dapr client accept a cancellation token and by default, if the operation is canceled, you will get an OperationCanceledException. However, if you choose to initialize and pass in your own GrpcChannelOptions to the client builder, then unless you enable the [ThrowOperationCanceledOnCancellation setting](https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Net.Client.GrpcChannelOptions.html#Grpc_Net_Client_GrpcChannelOptions_ThrowOperationCanceledOnCancellation), the exception thrown would be an RpcException with StatusCode as Cancelled. To get an OperationCanceledException instead, refer to the code below:-
 ```c#
            var httpClient = new HttpClient();
            var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
                .Build();
```
