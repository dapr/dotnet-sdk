# Example - Configuration APIs

This example demonstrates the Configuration APIs in Dapr.
It demonstrates the following APIs:
- **configuration**: Get configuration from statestore
- **configuration**: Subscribe Configuration

> **Note:** Make sure to use the latest proto bindings

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Overview
This example shows the usage of two different Configuration APIs. The GetConfiguration call and
the SubscribeConfiguration call. Both of these calls can be handled in two different ways.

### Dapr Client Call
Both the Get and Subscribe APIs can be called directly through the DaprClient. The Get response
can be read directly, however, the Subscribe response is recommended to be passed through to an alternate
thread. This is due to the fact that it is a streaming call and as such, it is not known when the
stream will stop. If it was read on the main thread, you could be waiting for it forever.

The SDK provides a utility object to help with this, the `ConfigurationWatcher`. This will handle
creating/monitoring an additional thread for you. Note that both the Get and Subscribe API results can
be passed to the `ConfigurationWatcher` to simplify the number of sources you have to watch if desired.

To stop a subscription, simply use the Unsubscribe API with the Id returned by the Subscribe API. Note
that the Id is only returned when the subscription receives an update.

#### GetConfiguration Example
```csharp
var configItems = await client.GetConfiguration(configStore, new List<string>() { queryKey });

foreach (var item in configItems.Items)
{
    logger.LogInformation($"Got configuration item:\nKey: {item.Key}\nValue: {item.Value}\nVersion: {item.Version}");
}
```

#### SubscribeConfiguration Example
```csharp
var watcher = new ConfigurationWatcher();
var source = await client.SubscribeConfiguration(configStore, new List<string>() { queryKey });
watcher.SubscribeToSource(source);

logger.LogInformation("Watching configuration for 1 minute.");
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
await Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        logger.LogInformation($"Greeting from the configuration: {watcher["greeting"]}");
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}, cts.Token);

await client.UnsubscribeConfiguration(configStore, source.Id);
```

### AspNet Configuration Extension
The SDK also provides an extension that allows the Configuration API to be included with the existing Application Configuration paradigm. To utilize them this way, simply include them in your host construction. Note that the values are first fetched without streaming. This is because the streaming values are only included in the configuration after the first update. This simply fetches the initial values and loads them into the configuration before subscribing to them.

```csharp
return Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        // Get the initial value and continue to watch it for changes.
        config.AddDaprConfigurationStore("redisconfig", new List<string>() { "greeting", "response" }, client, TimeSpan.FromSeconds(20));
        config.AddStreamingDaprConfigurationStore("redisconfig", new List<string>() { "greeting", "response" }, client,
            TimeSpan.FromSeconds(20), cancellationToken: cts.Token);
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
    logger.LogInformation($"Getting values from Configuration Extension, watched values ['greeting', 'response'].");

    logger.LogInformation($"Greeting from extension: {configuration["greeting"]}");
    logger.LogInformation($"Response from extension: {configuration["response"]}");

    return Task.CompletedTask;
}
```

## Store the configuration in configurationstore

This must be executed before starting the application.

```bash
docker exec dapr_redis redis-cli SET greeting "Hello there.||1"
docker exec dapr_redis redis-cli SET response "General Kenobi.||1"
```

## Run the example

Change directory to this folder:

```bash
cd examples/Client/ConfigurationApi
```

To run this example, use the following command:


```bash
dapr run --app-id configexample --components-path ./Components -- dotnet run
```

## Call the Controller
### Get Configuration
Using curl or the Dapr CLI, call the Get Configuration API.

```bash
curl 'http://localhost:5010/configuration/get/redisconfig/greeting'
```

You should see the following output from the application:

```
== APP ==       Got configuration item:
== APP ==       Key: greeting
== APP ==       Value: Hello there.
== APP ==       Version: 1
```

### Subscribe Configuration
This step requires multiple terminals.

#### Watch the Configuration
The configuration values can be watched for 1 minute by invoking the following URL.

```bash
curl 'http://localhost:5010/configuration/subscribe/redisconfig/response'
```

This will provide an output like the one seen below. Note, for the value to be recognized, you must first set the value. This is because
the subscribe API only notifies the callee when an item has changed. If you want to retrieve constant values, refer to the `GetConfiguration` API.

```
== APP ==       Subscribing to redisconfig watching response.
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Watching configuration for 1 minute.
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       No updates received to watched key.
== APP ==       Watched key: response -> Ah, the Negotiator.
```

Also notice that once a minute has elapsed, you will see that the subscription is cancelled.

```
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Cancelling subscription.
```

#### Update the Configuration
Using `docker` we can set some new configuration values. This will be reflected in the output from the command above.

```bash
docker exec dapr_redis redis-cli SET response "Ah, the Negotiator.||1"
```

### Configuration Extension
Using curl or the Dapr CLI, call the endpoint that prints configuration from the extension.

```bash
curl 'http://localhost:5010/configuration/extension'
```

You should see the following output from the application:

```
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Getting values from Configuration Extension, watched values ['greeting', 'response'].
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Greeting from extension: Hello there.
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Response from extension: General Kenobi.
```

If you have already updated the `response` value, you will see that the value is also updated in the configuration maintained by the extension.

```
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Getting values from Configuration Extension, watched values ['greeting', 'response'].
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Greeting from extension: Hello there.
== APP == info: ConfigurationApi.Controllers.ConfigurationController[0]
== APP ==       Response from extension: Ah, the Negotiator.
```

<!-- END_STEP -->