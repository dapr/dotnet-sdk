# ASP.NET Core Grpc Service Sample

This sample shows using Dapr with [ASP.NET Core Grpc Service](https://docs.microsoft.com/en-us/aspnet/core/grpc/aspnetcore). This application is a simple and not-so-secure banking application. The application uses the Dapr state-store for its data storage.

It exposes the following endpoints over GRPC:
 - `/getaccount`: Get the account information for the account specified by `id`
 - `/deposit`: Accepts a Protobuf payload to deposit money to an account
 - `/withdraw`: Accepts a Protobuf payload to withdraw money from an account

The application also registers for pub/sub with the `deposit` and `withdraw` topics.

 ## Running the Sample

 To run the sample locally run this command in this project root directory:
 ```sh
 dapr run --app-id grpcsample --app-port 5050 --app-protocol grpc -- dotnet run
 ```

 The application will listen on port 5050 for GRPC.

*NOTE: Because of this [bug](https://github.com/dapr/dapr/issues/1883), only can use port 5050 without TLS.*

 ### Client Examples

See InvokeGrpcBalanceServiceOperationAsync, InvokeGrpcDepositServiceOperationAsync and InvokeGrpcWithdrawServiceOperationAsync on DaprClient project.

 ## Code Samples

*All of the interesting code in this sample is in Startup.cs and Services/BankingService.cs*

```C#
public void ConfigureServices(IServiceCollection services)
{
    services.AddGrpc();

    services.AddDaprClient();
}
 ```

`AddDaprClient()` registers the Dapr integration with grpc service. This also registers the `DaprClient` service with the dependency injection container. This service can be used to interact with the Dapr state-store.

---


```C#
app.UseEndpoints(endpoints =>
{
    endpoints.MapGrpcService<BankingService>();

    ...
});
```

`MapGrpcService()` exposes BankingService grpc service into ASP.NET Core route endpoints.

---

```C#
public class BankingService : AppCallback.AppCallbackBase
{
    ...
}
```

You need to inherit AppCallback.AppCallbackBase that will be called by the Dapr runtime to invoke method, register for pub/sub topics and register bindings.

---

```C#
public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
{
    ...
}
```

You need to implement this overridable method to support Dapr's service invocation.

---

```C#
public override Task<ListTopicSubscriptionsResponse> ListTopicSubscriptions(Empty request, ServerCallContext context)
{
    ...
}
```

You need to implement this overridable method to support Dapr's pub/sub topic register.

---

```C#
public override async Task<TopicEventResponse> OnTopicEvent(TopicEventRequest request, ServerCallContext context)
{
    ...
}
```

You need to implement this overridable method to support Dapr's pub/sub topic handle.