# ASP.NET Core Controller example

This sample shows using Dapr with ASP.NET Core controllers. This application is a simple and not-so-secure banking application. The application uses the Dapr state-store for its data storage.

It exposes the following endpoints over HTTP:
 - GET `/{account}`: Get the balance for the account specified by `id`
 - POST `/deposit`: Accepts a JSON payload to deposit money to an account
 - POST `/withdraw`: Accepts a JSON payload to withdraw money from an account

The application also registers for pub/sub with the `deposit` and `withdraw` topics.

## Prerequisitess

- [.NET Core SDK](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

 ## Running the example

 To run the sample locally run this command in this project root directory:
 ```sh
 dapr run --app-id controller --app-port 5000 -- dotnet run
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
```txt
{"id":"17","balance":2}
```

 ---

 **Withdraw Money (pubsub)**
 
 Publish events using Dapr cli:
 
 On Linux, MacOS:
```sh
dapr publish --pubsub pubsub --publish-app-id controller -t withdraw -d '{"id": "17", "amount": 15 }'
```
On Windows:
 ```sh
 dapr publish --pubsub pubsub --publish-app-id controller -t withdraw -d "{\"id\": \"17\", \"amount\": 15 }"
 ```
 ---

**Deposit Money (pubsub)**
Publish events using Dapr cli:
On Linux, MacOS:
```sh
dapr publish --pubsub pubsub --publish-app-id controller -t deposit -d '{"id": "17", "amount": 15 }'
```
On Windows:
 ```sh
 dapr publish --pubsub pubsub --publish-app-id controller -t deposit -d "{\"id\": \"17\", \"amount\": 15 }"
```
 ---

 ## Code Samples

*All of the interesting code in this sample is in Startup.cs and Controllers/SampleController.cs*

```C#
 public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers().AddDapr(builder => 
        builder.UseJsonSerializationOptions(
            new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            }));

    ...
}
 ```

 `AddDapr()` registers the Dapr integration with controllers. This also registers the `DaprClient` service with the dependency injection container (using the sepcified `DaprClientBuilder` for settings options). This service can be used to interact with the dapr runtime (e.g. invoke services, publish messages, interact with a state-store, ...).

---

```C#
app.UseCloudEvents();
```

`UseCloudEvents()` registers the Cloud Events middleware in the request processing pipeline. This middleware will unwrap requests with Content-Type `application/cloudevents+json` so that model binding can access the event payload in the request body directly. This is recommended when using pub/sub unless you have a need to process the event metadata yourself.

---

```C#
app.UseEndpoints(endpoints =>
{
    endpoints.MapSubscribeHandler();
    endpoints.MapControllers();
});
```

`MapSubscribeHandler()` registers an endpoint that will be called by the Dapr runtime to register for pub/sub topics. This is is not needed unless using pub/sub.

---

```C#
[Topic("pubsub", "deposit")]
[HttpPost("deposit")]
public async Task<ActionResult<Account>> Deposit(...)
{
    ...
}
```

`[Topic(...)]` associates a pub/sub named `pubsub` (this is the default configured by the Dapr CLI) pub/sub topic `deposit` with this endpoint.

---

```C#
[HttpGet("{account}")]
public ActionResult<Account> Get(StateEntry<Account> account)
{
    if (account.Value is null)
    {
        return NotFound();
    }

    return account.Value;
}
```

Dapr's controller integration can automatically bind data from the state-store to an action parameter. Since the parameter's name is `account` the value of the account route-value will be used as the key. The data is stored in the state-store as JSON and will be deserialized as an object of type `Account`.

This could alternatively be written as:

```C#
[HttpGet("{account}")]
public ActionResult<Account> Get([FromState] Account account)
{
    ...
}

[HttpGet("{id}")]
public ActionResult<Account> Get([FromState("id")] Account account)
{
    ...
}
```

Using `[FromState]` allows binding a data type directly without using `StateEntry<>`. `[FromState(...)]` can also be used to specify which route-value contains the state-store key.

---

```C#
[Topic("pubsub", "deposit")]
[HttpPost("deposit")]
public async Task<ActionResult<Account>> Deposit(Transaction transaction, [FromServices] StateClient stateClient)
{
    var state = await stateClient.GetStateEntryAsync<Account>(transaction.Id);
    state.Value.Balance += transaction.Amount;
    await state.SaveAsync();
    return state.Value;
}
```

The `StateClient` can be retrieved from the dependency injection container, and can be used to imperatively access the state-store.

`state.SaveAsync()` can be used to save changes to a `StateEntry<>`.
