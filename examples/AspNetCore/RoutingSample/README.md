# ASP.NET Core routing example

This sample shows using Dapr with ASP.NET Core routing. This application is a simple and not-so-secure banking application. The application uses the Dapr state-store for its data storage.

It exposes the following endpoints over HTTP:
 - GET `/{id}`: Get the balance for the account specified by `id`
 - POST `/deposit`: Accepts a JSON payload to deposit money to an account
 - POST `/withdraw`: Accepts a JSON payload to withdraw money from an account

The application also registers for pub/sub with the `deposit` and `withdraw` topics.

## Prerequisites

- [.NET Core SDK](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Running the Sample

 To run the sample locally run this command in this project root directory:
 ```sh
 dapr run --app-id routing --app-port 5000 -- dotnet run
 ```

 The application will listen on port 5000 for HTTP.

 *Note: For Running the sample in ISS express, change the launchsettings.json to use 127.0.0.1 instead of localhost.*

 ### Examples

**Deposit Money**

On Linux, MacOS:
 ```sh
curl -X POST http://127.0.0.1:5000/deposit \
        -H 'Content-Type: application/json' \
        -d '{ "id": "17", "amount": 12 }'
 ```
On Windows:
 ```sh
 curl -X POST http://127.0.0.1:5000/deposit -H "Content-Type: application/json" -d "{ \"id\": \"17\", \"amount\": 12 }"
 ```

Or, we can also do this using the Visual Studio Code [Rest Client Plugin](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)

[sample.http](sample.http)
```http
POST http://127.0.0.1:5000/deposit
Content-Type: application/json

{ "id": "17", "amount": 12 }
```

Output:
```txt
 {"id":"17","balance":12}
```

 ---

**Withdraw Money**
On Linux, MacOS:
 ```sh
curl -X POST http://127.0.0.1:5000/withdraw \
        -H 'Content-Type: application/json' \
        -d '{ "id": "17", "amount": 10 }'
 ```
On Windows:
 ```sh
 curl -X POST http://127.0.0.1:5000/withdraw -H "Content-Type: application/json" -d "{ \"id\": \"17\", \"amount\": 10 }"
 ```

or using the Visual Studio Code [Rest Client Plugin](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)

[sample.http](sample.http)
```http
POST http://127.0.0.1:5000/withdraw
Content-Type: application/json

{ "id": "17", "amount": 5 }
```

 Output:
```txt
{"id":"17","balance":2}
```

 ---

**Get Balance**

```sh
curl http://127.0.0.1:5000/17
```

or using the Visual Studio Code [Rest Client Plugin](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)

[sample.http](sample.http)
```http
GET http://127.0.0.1:5000/17
```

Output:
```txt
{"id":"17","balance":2}
```

 ---

 **Withdraw Money (pubsub)**
 Publish events using Dapr cli:
On Linux, MacOS:
```sh
  dapr publish --pubsub pubsub --publish-app-id routing -t withdraw -d '{"id": "17", "amount": 15 }'
```

On Windows:
 ```sh
 dapr publish --pubsub pubsub --publish-app-id routing -t withdraw -d "{\"id\": \"17\", \"amount\": 15 }"
 ```

 ---

**Deposit Money (pubsub)**
Publish events using Dapr cli:
On Linux, MacOS:
```sh
  dapr publish --pubsub pubsub --publish-app-id routing -t deposit -d '{"id": "17", "amount": 15 }'
```
On Windows:
 ```sh
 dapr publish --pubsub pubsub --publish-app-id routing -t deposit -d "{\"id\": \"17\", \"amount\": 15 }"
 ```
 ---

## Code Samples

*All of the interesting code in this sample is in Startup.cs*

 ```C#
public void ConfigureServices(IServiceCollection services)
{
    services.AddDaprClient(builder =>
        builder.UseJsonSerializationOptions(
            new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            }));

    ...
}
 ```

 `AddDaprClient()` registers the `DaprClient` service with the dependency injection container (using the sepcified `DaprClientBuilder` for settings options). This service can be used to interact with the dapr runtime (e.g. invoke services, publish messages, interact with a state-store, ...).

---

```C#
app.UseCloudEvents();
```

`UseCloudEvents()` registers the Cloud Events middleware in the request processing pipeline. This middleware will unwrap requests with Content-Type `application/cloudevents+json` so that application code can access the event payload in the request body directly. This is recommended when using pub/sub unless you have a need to process the event metadata yourself.

---

```C#
app.UseEndpoints(endpoints =>
{
    endpoints.MapSubscribeHandler();

    endpoints.MapGet("{id}", Balance);
    endpoints.MapPost("deposit", Deposit).WithTopic(PubsubName, "deposit");
    endpoints.MapPost("withdraw", Withdraw).WithTopic(PubsubName, "withdraw");
});
```

`MapSubscribeHandler()` registers an endpoint that will be called by the Dapr runtime to register for pub/sub topics. This is is not needed unless using pub/sub.

`MapGet(...)` and `MapPost(...)` are provided by ASP.NET Core routing - these are used to setup endpoints to handle HTTP requests.

`WithTopic(...)` associates an endpoint with a pub/sub topic.

---

```C#
async Task Balance(HttpContext context)
{
    var client = context.RequestServices.GetRequiredService<StateClient>();

    var id = (string)context.Request.RouteValues["id"];
    var account = await client.GetStateAsync<Account>(id);
    if (account == null)
    {
        context.Response.StatusCode = 404;
        return;
    }

    context.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
}
```

Here `GetRequiredService<StateClient>()` is used to retrieve the `StateClient` from the service provider.

`client.GetStateAsync<Account>(id)` is used to retrieve an `Account` object from that state-store using the key in the variable `id`. The `Account` object stored in the state-store as JSON. If no entry is found for the specified key, then `null` will be returned.

---

```C#
await client.SaveStateAsync(transaction.Id, account);
```

`SaveStateAsync(...)` is used to save data to the state-store. 
