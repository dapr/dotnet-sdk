# ASP.NET Core Controller Sample

This sample shows using Dapr with ASP.NET Core routing. This application is a simple and not-so-secure banking application. The application uses the Dapr state-store for its data storage.

It exposes the following endpoints over HTTP:
 - GET `/{account}`: Get the balance for the account specified by `id`
 - POST `/deposit`: Accepts a JSON payload to deposit money to an account
 - POST `/withdraw`: Accepts a JSON payload to withdraw money from an account

The application also registers for pub-sub with the `deposit` and `withdraw` topics.

 ## Running the Sample

 To run the sample locally run this comment in this directory:
 ```sh
 dapr run --app-id routing --app-port 5000 dotnet run
 ```

 The application will listen on port 5000 for HTTP.

 ### Examples

**Deposit Money**

 ```sh
curl -X POST http://localhost:5000/deposit \
        -H 'Content-Type: application/json' \
        -d '{ "id": "17", "amount": 12 }'
 ```

```txt
 {"id":"17","balance":12}
```

 ---

**Withdraw Money**

 ```sh
curl -X POST http://localhost:5000/withdraw \
        -H 'Content-Type: application/json' \
        -d '{ "id": "17", "amount": 10 }'
 ```

```txt
{"id":"17","balance":2}
```

 ---

**Get Balance**

```sh
curl http://localhost:5000/17
```

```txt
{"id":"17","balance":2}
```

 ---

 **Withdraw Money (pubsub)**

```sh
dapr publish -t withdraw -p '{"id": "17", "amount": 15 }'
```

 ---

**Deposit Money (pubsub)**

```sh
dapr publish -t deposit -p '{"id": "17", "amount": 15 }'
```

 ---

 ## Code Samples

*All of the interesting code in this sample is in Startup.cs and Controllers/SampleController.cs*

```C#
 public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers().AddDapr();

    ...
}
 ```

 `AddDapr()` registers the Dapr integration with controllers. This also registers the `StateClient` service with the dependency injection container. This service can be used to interact with the Dapr state-store.

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

`MapSubscribeHandler()` registers an endpoint that will be called by the Dapr runtime to register for pub-sub topics. This is is not needed unless using pub-sub.

---

```C#
[Topic("deposit")]
[HttpPost("deposit")]
public async Task<ActionResult<Account>> Deposit(...)
{
    ...
}
```

`[Topic(...)]` associates a pub-sub topic with this endpoint.

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
[Topic("deposit")]
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