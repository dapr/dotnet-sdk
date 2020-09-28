# Dapr Client Sample
The client sample shows how to make Dapr calls to publish events, save state, get state and delete state using a Dapr client apis. 

## Prerequistes
* [.Net Core SDK 3.1](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)


 ## Running the Sample

 To run the sample locally run this command in GrpcClient directory:
 ```sh
 dapr run --app-id gRPC_Client dotnet run
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
Done
 ```

## Invoking Services
This solution contains a sample [RoutingSample service](..\..\AspNetCore\RoutingSample), which implements a simple banking application in ASP.NET core.
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
            
            // The HTTPExtension object is needed to specify additional information such as the HTTP verb and an optional query string, because the receiving service is listening on HTTP.  If it were listening on gRPC, it is not needed.
            HTTPExtension httpExtension = new HTTPExtension()
            {
                Verb = HTTPVerb.Post
            };

            await client.InvokeMethodAsync<object>("routing", "Withdraw", data, httpExtension);
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
