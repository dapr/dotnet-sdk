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
 Logs form application will show following in the command window:
```sh
  Published Event!
  Saved State!
  Got State: my data
  Deleted State!
 ```

## Invoking Services
This solution contains a sample REST *RoutingSample* service, which implements a simple banking application in ASP.NET core.
The service provides following operations:
- balance
- withdraw
- deposite

The service is a typical HTTP/S REST service. However, by using **dapr**, another application can invoke an operation on this service by using not only REST.
Following sample demonstrates how to use the gRPC with Dapr .NET SDK to invoke REST service operations.

Operation *balance* is a GET-operation mapped as (see startup.cs in the *RoutingService*):
 ```c#
 endpoints.MapGet("{id}", Balance);
 ```

It can be invoked via HTTTP/S by using following url:
 ```
curl -X GET http://127.0.0.1:5000/17
 ```

If you are developing the .NET client and want to invoke this operation via HTTP/S, you can also use *HttpClient* API.
The **dapr** supports out-of-the-box invokation of the service operation via gRPC.
To demonstrate this, take a look on the method *InvokeBalanceServiceOperationAsync*.
Services are typically invoked with the following code:

```c#
var metaData = new Dictionary<string, string>();
metaData.Add("http.verb", "GET");

var res = await client.InvokeMethodAsync<object serviceName, "17", metadata);
```
In this example, the HttpGet method is onvoked on the service with name *serviceName*. This argument specifies the name of the service, which was used when in conjunction with *dapr run –app-id serviceName*.The *route* argument specifies the route of the service endpoint.
For example, if your service is the Rest service, the route is typically dened by attribute *HttpPost(“route”)* or *HttpGet(“route”)*. Last argument is called metadata and it specifies GET operation in tis example.


Second service operation *withdraw* can be invoked by HTTP/S POST request, but also triggered as a *cloud event* by publishing the event to the topic with name 'withdraw'.
To enable this, the operation is mapped as:
 ```c#
endpoints.MapPost("withdraw", Withdraw).WithTopic("withdraw");
 ```
and it can also be invoked (triggered) by following url:

 ``` 
curl -X POST http://127.0.0.1:5000/withdraw -H "Content-Type: application/json" -d "{ \"id\": \"17\", \"amount\": 1 }"
 ```


The method *InvokeWithdrawServiceOperationAsync* demonstrates how to use DAPR .NET SDK to invoke a REST/POST operation via gRPC.

 ```c#        
            ...

            metaData.Add("http.verb", "POST");

            await client.InvokeMethodAsync<object>("routing", "Withdraw", new { id = "17", amount = (decimal)10 }, metaData);
 ```

Because, the same operation subscribes events on the *withdraw* topic, it can be invoked by event:
``` 
dapr publish -t withdraw -p '{"id": "17", "amount": 15 }'
``` 

Operation *deposite* can be invoked by HTTP/S POST request and also triggered as a *cloud event* by publishing the event to the topic with name 'deposite'.
It is mapped as:
 ```c#
endpoints.MapPost("deposit", Withdraw).WithTopic("deposit");
 ```
You can also use a **dapr** cli to publish the event to invoke te operation *deposit*: 
``` 
dapr publish -t deposit -p '{"id": "17", "deposit": 15 }'
 ``` 

The method *PublishDepositeEventToRoutingSampleAsync* demonstrates how to publish an event to the dapr runtime, which triggers the REST operation 'deposit'.
 ```c#
            await client.PublishEventAsync("deposit", new  { id = "17", amount = (decimal)10, });          
 ```