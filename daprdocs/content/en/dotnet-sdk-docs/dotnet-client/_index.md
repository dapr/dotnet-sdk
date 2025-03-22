---
type: docs
title: "Getting started with the Dapr client .NET SDK"
linkTitle: "Client"
weight: 20000
description: How to get up and running with the Dapr .NET SDK
no_list: true
---

The Dapr client package allows you to interact with other Dapr applications from a .NET application.

{{% alert title="Note" color="primary" %}}
 If you haven't already, [try out one of the quickstarts]({{< ref quickstarts >}}) for a quick walk-through on how to use the Dapr .NET SDK with an API building block.

{{% /alert %}}


## Building blocks

The .NET SDK allows you to interface with all of the [Dapr building blocks]({{< ref building-blocks >}}).

### Invoke a service

#### HTTP
You can either use the `DaprClient` or `System.Net.Http.HttpClient` to invoke your services.

{{% alert title="Note" color="primary" %}}
 You can also [invoke a non-Dapr endpoint using either a named `HTTPEndpoint` or an FQDN URL to the non-Dapr environment]({{< ref "howto-invoke-non-dapr-endpoints.md#using-an-httpendpoint-resource-or-fqdn-url-for-non-dapr-endpoints" >}}).

{{% /alert %}}


{{< tabs SDK HTTP>}}

{{% codetab %}}
```csharp
using var client = new DaprClientBuilder().
                UseTimeout(TimeSpan.FromSeconds(2)). // Optionally, set a timeout
                Build(); 

// Invokes a POST method named "deposit" that takes input of type "Transaction"
var data = new { id = "17", amount = 99m };
var account = await client.InvokeMethodAsync<object, Account>("routing", "deposit", data, cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
```
{{% /codetab %}}

{{% codetab %}}
```csharp
var client = DaprClient.CreateInvokeHttpClient(appId: "routing");

// To set a timeout on the HTTP client:
client.Timeout = TimeSpan.FromSeconds(2);

var deposit = new Transaction  { Id = "17", Amount = 99m };
var response = await client.PostAsJsonAsync("/deposit", deposit, cancellationToken);
var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
```
{{% /codetab %}}
{{< /tabs >}}

#### gRPC
You can use the `DaprClient` to invoke your services over gRPC.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var invoker = DaprClient.CreateInvocationInvoker(appId: myAppId, daprEndpoint: serviceEndpoint);
var client = new MyService.MyServiceClient(invoker);

var options = new CallOptions(cancellationToken: cts.Token, deadline: DateTime.UtcNow.AddSeconds(1));
await client.MyMethodAsync(new Empty(), options);

Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
```

- For a full guide on service invocation visit [How-To: Invoke a service]({{< ref howto-invoke-discover-services.md >}}).

### Save & get application state

```csharp
var client = new DaprClientBuilder().Build();

var state = new Widget() { Size = "small", Color = "yellow", };
await client.SaveStateAsync(storeName, stateKeyName, state, cancellationToken: cancellationToken);
Console.WriteLine("Saved State!");

state = await client.GetStateAsync<Widget>(storeName, stateKeyName, cancellationToken: cancellationToken);
Console.WriteLine($"Got State: {state.Size} {state.Color}");

await client.DeleteStateAsync(storeName, stateKeyName, cancellationToken: cancellationToken);
Console.WriteLine("Deleted State!");
```

### Query State (Alpha)

```csharp
var query = "{" +
                "\"filter\": {" +
                    "\"EQ\": { \"value.Id\": \"1\" }" +
                "}," +
                "\"sort\": [" +
                    "{" +
                        "\"key\": \"value.Balance\"," +
                        "\"order\": \"DESC\"" +
                    "}" +
                "]" +
            "}";

var client = new DaprClientBuilder().Build();
var queryResponse = await client.QueryStateAsync<Account>("querystore", query, cancellationToken: cancellationToken);

Console.WriteLine($"Got {queryResponse.Results.Count}");
foreach (var account in queryResponse.Results)
{
    Console.WriteLine($"Account: {account.Data.Id} has {account.Data.Balance}");
}
```

- For a full list of state operations visit [How-To: Get & save state]({{< ref howto-get-save-state.md >}}).

### Publish messages

```csharp
var client = new DaprClientBuilder().Build();

var eventData = new { Id = "17", Amount = 10m, };
await client.PublishEventAsync(pubsubName, "deposit", eventData, cancellationToken);
Console.WriteLine("Published deposit event!");
```

- For a full list of state operations visit [How-To: Publish & subscribe]({{< ref howto-publish-subscribe.md >}}).
- Visit [.NET SDK examples](https://github.com/dapr/dotnet-sdk/tree/master/examples/Client/PublishSubscribe) for code samples and instructions to try out pub/sub

### Interact with output bindings

```csharp
using var client = new DaprClientBuilder().Build();

// Example payload for the Twilio SendGrid binding
var email = new 
{
    metadata = new 
    {
        emailTo = "customer@example.com",
        subject = "An email from Dapr SendGrid binding",    
    }, 
    data =  "<h1>Testing Dapr Bindings</h1>This is a test.<br>Bye!",
};
await client.InvokeBindingAsync("send-email", "create", email);
```

- For a full guide on output bindings visit [How-To: Use bindings]({{< ref howto-bindings.md >}}).

### Retrieve secrets

{{< tabs Multi-value-secret Single-value-secret >}}

{{% codetab %}}

```csharp
var client = new DaprClientBuilder().Build();

// Retrieve a key-value-pair-based secret - returns a Dictionary<string, string>
var secrets = await client.GetSecretAsync("mysecretstore", "key-value-pair-secret");
Console.WriteLine($"Got secret keys: {string.Join(", ", secrets.Keys)}");
```

{{% /codetab %}}

{{% codetab %}}

```csharp
var client = new DaprClientBuilder().Build();

// Retrieve a key-value-pair-based secret - returns a Dictionary<string, string>
var secrets = await client.GetSecretAsync("mysecretstore", "key-value-pair-secret");
Console.WriteLine($"Got secret keys: {string.Join(", ", secrets.Keys)}");

// Retrieve a single-valued secret - returns a Dictionary<string, string>
// containing a single value with the secret name as the key
var data = await client.GetSecretAsync("mysecretstore", "single-value-secret");
var value = data["single-value-secret"]
Console.WriteLine("Got a secret value, I'm not going to be print it, it's a secret!");
```

{{% /codetab %}}

{{< /tabs >}}

- For a full guide on secrets visit [How-To: Retrieve secrets]({{< ref howto-secrets.md >}}).

### Get Configuration Keys
```csharp
var client = new DaprClientBuilder().Build();

// Retrieve a specific set of keys.
var specificItems = await client.GetConfiguration("configstore", new List<string>() { "key1", "key2" });
Console.WriteLine($"Here are my values:\n{specificItems[0].Key} -> {specificItems[0].Value}\n{specificItems[1].Key} -> {specificItems[1].Value}");

// Retrieve all configuration items by providing an empty list.
var specificItems = await client.GetConfiguration("configstore", new List<string>());
Console.WriteLine($"I got {configItems.Count} entires!");
foreach (var item in configItems)
{
    Console.WriteLine($"{item.Key} -> {item.Value}")
}
```

### Subscribe to Configuration Keys
```csharp
var client = new DaprClientBuilder().Build();

// The Subscribe Configuration API returns a wrapper around an IAsyncEnumerable<IEnumerable<ConfigurationItem>>.
// Iterate through it by accessing its Source in a foreach loop. The loop will end when the stream is severed
// or if the cancellation token is cancelled.
var subscribeConfigurationResponse = await daprClient.SubscribeConfiguration(store, keys, metadata, cts.Token);
await foreach (var items in subscribeConfigurationResponse.Source.WithCancellation(cts.Token))
{
    foreach (var item in items)
    {
        Console.WriteLine($"{item.Key} -> {item.Value}")
    }
}
```

### Distributed lock (Alpha)

#### Acquire a lock

```csharp
using System;
using Dapr.Client;

namespace LockService
{
    class Program
    {
        [Obsolete("Distributed Lock API is in Alpha, this can be removed once it is stable.")]
        static async Task Main(string[] args)
        {
            var daprLockName = "lockstore";
            var fileName = "my_file_name";
            var client = new DaprClientBuilder().Build();
     
            // Locking with this approach will also unlock it automatically, as this is a disposable object
            await using (var fileLock = await client.Lock(DAPR_LOCK_NAME, fileName, "random_id_abc123", 60))
            {
                if (fileLock.Success)
                {
                    Console.WriteLine("Success");
                }
                else
                {
                    Console.WriteLine($"Failed to lock {fileName}.");
                }
            }
        }
    }
}
```

#### Unlock an existing lock

```csharp
using System;
using Dapr.Client;

namespace LockService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var daprLockName = "lockstore";
            var client = new DaprClientBuilder().Build();

            var response = await client.Unlock(DAPR_LOCK_NAME, "my_file_name", "random_id_abc123"));
            Console.WriteLine(response.status);
        }
    }
}
```

## Sidecar APIs
### Sidecar Health
The .NET SDK provides a way to poll for the sidecar health, as well as a convenience method to wait for the sidecar to be ready.

#### Poll for health
This health endpoint returns true when both the sidecar and your application are up (fully initialized).

```csharp
var client = new DaprClientBuilder().Build();

var isDaprReady = await client.CheckHealthAsync();

if (isDaprReady) 
{
    // Execute Dapr dependent code.
}
```

#### Poll for health (outbound)
This health endpoint returns true when Dapr has initialized all its components, but may not have finished setting up a communication channel with your application.

This is best used when you want to utilize a Dapr component in your startup path, for instance, loading secrets from a secretstore.

```csharp
var client = new DaprClientBuilder().Build();

var isDaprComponentsReady = await client.CheckOutboundHealthAsync();

if (isDaprComponentsReady) 
{
    // Execute Dapr component dependent code.
}
```

#### Wait for sidecar
The `DaprClient` also provides a helper method to wait for the sidecar to become healthy (components only). When using this method, it is recommended to include a `CancellationToken` to
allow for the request to timeout. Below is an example of how this is used in the `DaprSecretStoreConfigurationProvider`.

```csharp
// Wait for the Dapr sidecar to report healthy before attempting use Dapr components.
using (var tokenSource = new CancellationTokenSource(sidecarWaitTimeout))
{
    await client.WaitForSidecarAsync(tokenSource.Token);
}

// Perform Dapr component operations here i.e. fetching secrets.
```

### Shutdown the sidecar
```csharp
var client = new DaprClientBuilder().Build();
await client.ShutdownSidecarAsync();
```

## Related links
- [.NET SDK examples](https://github.com/dapr/dotnet-sdk/tree/master/examples)
