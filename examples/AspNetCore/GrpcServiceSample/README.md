# ASP.NET Core gRPC service example

This sample shows using Dapr's GrpcBaseService. It build with [ASP.NET Core gRPC Service](https://docs.microsoft.com/en-us/aspnet/core/grpc/aspnetcore). This application is a simple and not-so-secure banking application. The application uses the Dapr state-store for its data storage.

It exposes the following endpoints over GRPC by custom attribute `GrpcInvoke`:
 - `/getaccount`: Get the account information for the account specified by `id`
 - `/deposit`: Accepts a Protobuf payload to deposit money to an account
 - `/withdraw`: Accepts a Protobuf payload to withdraw money from an account
 - `/close`: Close the account for the account specified by `id`

The application also registers for pub/sub with the `deposit` and `withdraw` topics by custom attribute `Topic`.

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Running the example

To run the sample locally run this command in this project root directory:

```sh
dapr run --app-id grpcsample --app-port 5050 --app-protocol grpc -- dotnet run
```

The application will listen on port 5050 for GRPC.

### Client Examples

See InvokeServiceGrpcExample in ServiceInvocation project.

## Code Samples

**All of the interesting code in this sample is in Startup.cs and Services/BankingService2.cs**

```C#
public void ConfigureServices(IServiceCollection services)
{
    services.AddGrpc();

    services.AddDaprClient();

    services.AddDaprGrpcService<BankingService2>();
}
```

`AddDaprClient()` registers the Dapr integration with grpc service. This also registers the `DaprClient` service with the dependency injection container. This service can be used to interact with the Dapr state-store.

`AddDaprGrpcService(BankingService2)` registers BankingService2 which derive from GrpcBaseService into default AppCallback default implementation grpc service.

---

```C#
app.UseEndpoints(endpoints =>
{
    endpoints.MapAppCallback();

    ...
});
```

`MapAppCallback()` exposes AppCallback default implementation grpc service into ASP.NET Core route endpoints.

---

```C#
public class BankingService2 : GrpcBaseService
{
    //...

    [GrpcInvoke]
    public async Task<Account> GetAccount(GetAccountRequest input)
    {
        //...
    }

    [GrpcInvoke]
    [Topic("pubsub", "deposit")]
    public async Task<Account> Deposit(Transaction transaction)
    {
        //...
    }

    [GrpcInvoke]
    [Topic("pubsub", "withdraw")]
    public async Task<Account> Withdraw(Transaction transaction)
    {
        //...
    }

    [GrpcInvoke]
    public async Task CloseAccount(GetAccountRequest input)
    {
        //...
    }
}
```

You need to inherit GrpcBaseService that will be called by the Dapr runtime to invoke method, register for pub/sub topics and register bindings.

---

```C#
[GrpcInvoke]
public async Task<Account> GetAccount(GetAccountRequest input)
{
    //...
}
```

You need to implement a public method with custom attribute `GrpcInvoke` to support Dapr's service invocation. And it must have one parameter which is IMessage.

---

```C#
[GrpcInvoke]
[Topic("pubsub", "deposit")]
public async Task<Account> Deposit(Transaction transaction)
{
    //...
}
```

You need to implement a public method with custom attribute `Topic` to support Dapr's pub/sub topic register and handle. Note that same public method can be flag with `GrpcInvoke`.
