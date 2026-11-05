# Example - Configuration APIs

This example demonstrates the Configuration APIs in Dapr.
It demonstrates the following APIs:
- Directly get a configuration value with `DaprClient.GetConfiguration`.
- Load initial values into ASP.NET Core configuration with
  `AddDaprConfigurationStore`.
- Stream updates into ASP.NET Core configuration with
  `AddStreamingDaprConfigurationStore`.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Overview
The sample reads `withdrawVersion` and `source` from the `redisconfig` component.
It also invokes `ControllerSample` to choose between the `/withdraw` and
`/withdraw.v2` service endpoints based on the current configuration.

| Method | Route | Behavior |
|--------|-------|----------|
| `GET` | `/configuration/get/{configStore}/{queryKey}` | Calls `GetConfiguration` and logs returned values. |
| `GET` | `/configuration/extension` | Logs `withdrawVersion` and `source` from `IConfiguration`. |
| `POST` | `/configuration/withdraw` | Invokes `controller/withdraw` or `controller/withdraw.v2` according to `withdrawVersion`. |

### Dapr Client Call
`ConfigurationController.GetConfiguration` calls the client directly and logs
the returned items.

#### GetConfiguration Example
```csharp
var configItems = await client.GetConfiguration(configStore, new List<string>() { queryKey });

foreach (var item in configItems.Items)
{
    logger.LogInformation($"Got configuration item:\nKey: {item.Key}\nValue: {item.Value.Value}\nVersion: {item.Value.Version}");
}
```

### AspNet Configuration Extension
The SDK also provides an extension that allows the Configuration API to be included with the existing Application Configuration paradigm. To utilize them this way, simply include them in your host construction. Note that the values are first fetched without streaming. This is because the streaming values are only included in the configuration after the first update. This simply fetches the initial values and loads them into the configuration before subscribing to them.

```csharp
return Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        // Get the initial value and continue to watch it for changes.
        config.AddDaprConfigurationStore("redisconfig", new List<string>() { "withdrawVersion" }, client, TimeSpan.FromSeconds(20));
        config.AddStreamingDaprConfigurationStore("redisconfig", new List<string>() { "withdrawVersion", "source" }, client, TimeSpan.FromSeconds(20));
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseStartup<Startup>();
    });
```

And then reference the `IConfiguration` object in whatever classes it is needed.

```csharp
private ILogger<ConfigurationController> logger;
private IConfiguration configuration;
private DaprClient client;

public ConfigurationController(ILogger<ConfigurationController> logger, IConfiguration configuration, [FromServices] DaprClient client)
{
    this.logger = logger;
    this.configuration = configuration;
    this.client = client;
}

[HttpGet("extension")]
public Task SubscribeAndWatchConfiguration()
{
    logger.LogInformation($"Getting values from Configuration Extension, watched values ['withdrawVersion', 'source'].");

    logger.LogInformation($"'withdrawVersion' from extension: {configuration["withdrawVersion"]}");
    logger.LogInformation($"'source' from extension: {configuration["source"]}");

    return Task.CompletedTask;
}
```

## Run the example

This example relies on the `ControllerSample`. All functionality won't be available without it running.

### Store configuration in `redisconfig`

> Note: This must be executed before starting the application. `GetConfiguration` treats missing configuration as an exceptional case.

```bash
docker exec dapr_redis redis-cli SET withdrawVersion "v1||1"
```

The above command creates an object with a key 'withdrawVersion' and a value of 'v1' in your redis store. The `||1` denotes the version of the key and is not necessary but is included for fullness.

### Start the ControllerSample

Change directory to this folder:

```bash
cd examples/AspNetCore/ControllerSample
```

To run the `ControllerSample`, execute the following command:

```bash
dapr run --app-id controller --app-port 5000 -- dotnet run
```

### Start the ConfigurationExample

Change directory to this folder:

```bash
cd examples/Client/ConfigurationApi
```

To run the `ConfigurationExample`, execute the following command:

```bash
dapr run --app-id configexample --resources-path ./Components -- dotnet run
```

### Get Configuration
Using curl or the Dapr CLI, call the Get Configuration API.

```bash
curl 'http://localhost:5010/configuration/get/redisconfig/withdrawVersion'
```

You should see the following output from the application:

```
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Querying Configuration with key: withdrawVersion
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Got configuration item:
== APP ==       Key: withdrawVersion
== APP ==       Value: v1
== APP ==       Version: 1
```

### Working with the SubscribeConfiguration API
> Note: This step requires multiple terminals.

This step will utilize the `ControllerSample` to show how a subscribed configuration can be used as a dynamic feature flag. The `ControllerSample` vends
several APIs, but the one we're interested in is the `withdraw` API which also has a `withdraw.v2` version.

First, we need to setup an account with some money in it. We'll do this with the `deposit` API. Run the following command:

```bash
curl -X POST http://127.0.0.1:5000/deposit -H "Content-Type: application/json" -d "{ \"id\": \"1\", \"amount\": 100000 }"
```

Now, we can start making requests. Without changing anything in the configuration, run the following command:

```bash
curl -X POST http://127.0.0.1:5010/configuration/withdraw -H "Content-Type: application/json" -d "{ \"id\": \"1\", \"amount\": 10 }"
```

You should the following output in your configuration app:

```bash
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Calling V1 Withdraw API: ControllerSample.Transaction
```

Now, let's move our application to use the v2 API. Do this by running the following command:

```bash
docker exec dapr_redis redis-cli SET withdrawVersion "v2||1"
```

Run the withdraw command again and you'll see an output like this from your app:

```bash
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Calling V2 Withdraw API - Id: 1 Amount: 10 Channel: local
```

We use the 'local' channel because nothing has been set in your configuration. Change the 'source' configuration item to 'mobile' and increase the withdraw to 100000. 
Doing this, we can see a new feature in the V2 API which is denying requests that are too large from mobile sources.

```bash
docker exec dapr_redis redis-cli SET source "mobile||1" 
curl -X POST http://127.0.0.1:5010/configuration/withdraw -H "Content-Type: application/json" -d "{ \"id\": \"1\", \"amount\": 100000 }"
```

After the withdraw command, you should see an error in your application:

```bash
== APP == fail: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Error executing withdrawal: An exception occurred while invoking method: 'withdraw.v2' on app-id: 'controller'
```

### Configuration Extension
Using curl or the Dapr CLI, call the endpoint that prints configuration from the extension.

```bash
curl 'http://localhost:5010/configuration/extension'
```

You should see an output similar to what is below. If you've changed the value of 'withdrawVersion' or 'source', you could see a different value:

```
== APP ==       Getting values from Configuration Extension, watched values ['withdrawVersion', 'source'].
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       'withdrawVersion' from extension: v2
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       'source' from extension: mobile
```
