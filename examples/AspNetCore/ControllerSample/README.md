# ASP.NET Core Controller example

This sample shows using Dapr with ASP.NET Core controllers. This application is a simple and not-so-secure banking application. The application uses the Dapr state-store for its data storage.

It exposes the following endpoints over HTTP:
 - GET `/{account}`: Get the balance for the account specified by `id`
 - POST `/deposit`: Accepts a JSON payload to deposit money to an account
 - POST `/multideposit`: Accepts a JSON payload to deposit money multiple times to a bulk subscribed topic
 - POST `/withdraw`: Accepts a JSON payload to withdraw money from an account

The application also registers for pub/sub with the `deposit`, `multideposit` and `withdraw` topics.

## Prerequisitess

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

 ## Running the example

 To run the sample locally run this command in this project root directory:
 ```sh
 dapr run --app-id controller --app-port 5000 -- dotnet run
 ```

 The application will listen on port 5000 for HTTP.

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
**Deposit Money multiple times to a bulk subscribed topic**

On Linux, MacOS:
```
curl -X POST http://127.0.0.1:5000/multideposit \
       -H 'Content-Type: application/json' \
       -d '{
   "entries":[
      {
         "entryId":"653dd9f5-f375-499b-8b2a-c4599bbd36b0",
         "event":{
            "data":{
               "amount":10,
               "id":"17"
            },
            "datacontenttype":"application/json",
            "id":"DaprClient",
            "pubsubname":"pubsub",
            "source":"Dapr",
            "specversion":"1.0",
            "topic":"multideposit",
            "type":"com.dapr.event.sent"
         },
         "metadata":null,
         "contentType":"application/cloudevents+json"
      },
      {
         "entryId":"7ea8191e-1e62-46d0-9ba8-ff6e571351cc",
         "event":{
            "data":{
               "amount":20,
               "id":"17"
            },
            "datacontenttype":"application/json",
            "id":"DaprClient",
            "pubsubname":"pubsub",
            "source":"Dapr",
            "specversion":"1.0",
            "topic":"multideposit",
            "type":"com.dapr.event.sent"
         },
         "metadata":null,
         "contentType":"application/cloudevents+json"
      }
   ],
   "id":"fa68c580-1b96-40d3-aa2c-04bab05e954e",
   "metadata":{
      "pubsubName":"pubsub"
   },
   "pubsubname":"pubsub",
   "topic":"multideposit",
   "type":"com.dapr.event.sent.bulk"
}'
```
Output:
```
{
   "statuses":[
      {
         "entryId":"653dd9f5-f375-499b-8b2a-c4599bbd36b0",
         "status":"SUCCESS"
      },
      {
         "entryId":"7ea8191e-1e62-46d0-9ba8-ff6e571351cc",
         "status":"SUCCESS"
      }
   ]
}
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
**Dead Letter Topic example (pubsub)**
Publish an event using the Dapr cli with an incorrect input, i.e. negative amount:

Deposit:
On Linux, MacOS:
```sh
dapr publish --pubsub pubsub --publish-app-id controller -t deposit -d '{"id": "17", "amount": -15 }'
```
On Windows:
 ```sh
 dapr publish --pubsub pubsub --publish-app-id controller -t deposit -d "{\"id\": \"17\", \"amount\": -15 }"
```

Withdraw:
 On Linux, MacOS:
```sh
dapr publish --pubsub pubsub --publish-app-id controller -t withdraw -d '{"id": "17", "amount": -15 }'
```
On Windows:
 ```sh
 dapr publish --pubsub pubsub --publish-app-id controller -t withdraw -d "{\"id\": \"17\", \"amount\": -15 }"
 ```
 
First a message is sent from a publisher on a `deposit` or `withdraw` topic. Dapr receives the message on behalf of a subscriber application, however the `deposit` or `withdraw` topic message fails to be delivered to the `/deposit` or `/withdraw` endpoint on the application, even after retries. As a result of the failure to deliver, the message is forwarded to the `amountDeadLetterTopic` topic which delivers this to the `/deadLetterTopicRoute` endpoint.

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
[Topic("pubsub", "multideposit", "amountDeadLetterTopic", false)]
[BulkSubscribe("multideposit")]
[HttpPost("multideposit")]
public async Task<ActionResult<BulkSubscribeAppResponse>> MultiDeposit([FromBody] BulkSubscribeMessage<BulkMessageModel<Transaction>> 
    bulkMessage, [FromServices] DaprClient daprClient)
```

`[BulkSubscribe(...)]` associates a topic with the name mentioned in the attribute with the ability to be bulk subscribed to. It can take additional parameters like `MaxMessagesCount` and `MaxAwaitDurationMs`.
If those parameters are not supplied, the defaults of 100 and 1000ms are set.

However, you need to use `BulkSubscribeMessage<BulkMessageModel<T>>` in the input and that you need to return the `BulkSubscribeAppResponse` as well.

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
