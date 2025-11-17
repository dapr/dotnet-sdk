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
 If you haven't already, [try out one of the quickstarts]({{% ref quickstarts %}}) for a quick walk-through on how to use the Dapr .NET SDK with an API building block.

{{% /alert %}}


## Building blocks

The .NET SDK allows you to interface with all of the [Dapr building blocks]({{% ref building-blocks %}}). 

{{% alert title="Note" color="primary" %}}

We will only include the dependency injection registration for the `DaprClient` in the first example 
(service invocation). In nearly all other examples, it's assumed you've already registered the `DaprClient` in your 
application in the latter examples and have injected an instance of `DaprClient` into your code as an instance named 
`client`.

{{% /alert %}}

### Invoke a service

#### HTTP
You can either use the `DaprClient` or `System.Net.Http.HttpClient` to invoke your services.

{{% alert title="Note" color="primary" %}}
 You can also [invoke a non-Dapr endpoint using either a named `HTTPEndpoint` or an FQDN URL to the non-Dapr environment]({{% ref "howto-invoke-non-dapr-endpoints.md#using-an-httpendpoint-resource-or-fqdn-url-for-non-dapr-endpoints" %}}).

{{% /alert %}}


{{< tabpane text=true >}}

{{% tab header="ASP.NET Core Project" %}}
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprClient();
var app = builder.Build();

using var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<DaprClient>();
 
// Invokes a POST method named "deposit" that takes input of type "Transaction"
var data = new { id = "17", amount = 99m };
var account = await client.InvokeMethodAsync<Account>("routing", "deposit", data, cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
```
{{% /tab %}}

{{% tab header="Console Project" %}}
using Microsoft.Extensins.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDaprClient();
var app = builder.Build();

using var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<DaprClient>();

// Invokes a POST method named "deposit" that takes input of type "Transaction"
var data = new { id = "17", amount = 99m };
var account = await client.InvokeMethodAsync<Account>("routing", "deposit", data, cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
{{% /tab %}}

{{% tab header="HTTP" %}}
```csharp
var client = DaprClient.CreateInvokeHttpClient(appId: "routing");

// To set a timeout on the HTTP client:
client.Timeout = TimeSpan.FromSeconds(2);

var deposit = new Transaction  { Id = "17", Amount = 99m };
var response = await client.PostAsJsonAsync("/deposit", deposit, cancellationToken);
var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
```
{{% /tab %}}
{{< /tabpane >}}

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

- For a full guide on service invocation visit [How-To: Invoke a service]({{% ref howto-invoke-discover-services.md %}}).

### Save & get application state

```csharp
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
var queryResponse = await client.QueryStateAsync<Account>("querystore", query, cancellationToken: cancellationToken);

Console.WriteLine($"Got {queryResponse.Results.Count}");
foreach (var account in queryResponse.Results)
{
    Console.WriteLine($"Account: {account.Data.Id} has {account.Data.Balance}");
}
```

- For a full list of state operations visit [How-To: Get & save state]({{% ref howto-get-save-state.md %}}).

### Publish messages

```csharp
var eventData = new { Id = "17", Amount = 10m, };
await client.PublishEventAsync(pubsubName, "deposit", eventData, cancellationToken);
Console.WriteLine("Published deposit event!");
```

- For a full list of state operations visit [How-To: Publish & subscribe]({{% ref howto-publish-subscribe.md %}}).
- Visit [.NET SDK examples](https://github.com/dapr/dotnet-sdk/tree/master/examples/Client/PublishSubscribe) for code samples and instructions to try out pub/sub

### Interact with output bindings

When calling `InvokeBindingAsync`, you have the option to handle serialization and encoding yourself, 
or to have the SDK serialize it to JSON and then encode it to bytes for you.

{{% alert title="Important" color="warning" %}}
Bindings differ in the shape of data they expect, take special note and ensure that the data you 
are sending is handled accordingly.
{{% /alert %}}

#### Manual serialization

For most scenarios, we advise that you use this overload of `InvokeBindingAsync` as it gives you clarity and control over 
how the data is being handled.

_In this example, the data is sent as the UTF-8 byte representation of the string._

```csharp
using var client = new DaprClientBuilder().Build();

var request = new BindingRequest("send-email", "create")
{
    Data = Encoding.UTF8.GetBytes("<h1>Testing Dapr Bindings</h1>This is a test.<br>Bye!"),
    Metadata =
    {
        { "emailTo", "customer@example.com" },
        { "subject", "An email from Dapr SendGrid binding" },
    },
}
await client.InvokeBindingAsync(request);
```

#### Automatic serialzation and encoding

_In this example, the data is sent as a UTF-8 encoded byte representation of the value serialized to JSON._

```csharp
using var client = new DaprClientBuilder().Build();

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

- For a full guide on output bindings visit [How-To: Use bindings]({{% ref howto-bindings.md %}}).

### Retrieve secrets
Prior to retrieving secrets, it's important that the outbound channel be registered and ready or the SDK will be unable
to communicate bidirectionally with the Dapr sidecar. The SDK provides a helper method intended to be used for this 
purpose called `CheckOutboundHealthAsync`. This isn't referring to outbound from the SDK to the runtime, so much as 
outbound from the Dapr runtime back into the client application using the SDK.

This method is simply opening a connection to the {{% ref "health_api#wait-for-specific-health-check-against-outbound-path" %}}
endpoint in the Dapr Health API and evaluating the HTTP status code returned to determine the health of the endpoint 
as reported by the runtime.

It's important to note that this and the `WaitForSidecarAsync` methods perform nearly identical operations; `WaitForSidecarAsync`
polls the `CheckOutboundHealthAsync` endpoint indefinitely until it returns a healthy status value. They are intended 
exclusively for situations like secrets or configuration retrieval. Using them in other scenarios will result in 
unintended behavior (e.g., the endpoint never being ready because there are no registered components that use an 
"outbound" channel).

This behavior will be changed in a future release and should only be relied on sparingly.


{{< tabpane text=true >}}

{{% tab header="Multi-value-secret" %}}

```csharp
// Get an instance of the DaprClient from DI
var client = scope.GetRequiredService<DaprClient>();

// Wait for the outbound channel to be established - only use for this scenario and not generally
await client.WaitForOutboundHealthAsync();

// Retrieve a key-value-pair-based secret - returns a Dictionary<string, string>
var secrets = await client.GetSecretAsync("mysecretstore", "key-value-pair-secret");
Console.WriteLine($"Got secret keys: {string.Join(", ", secrets.Keys)}");
```

{{% /tab %}}

{{% tab header="Single-value-secret" %}}

```csharp
// Get an instance of the DaprClient from DI
var client = scope.GetRequiredService<DaprClient>();

// Wait for the outbound channel to be established - only use for this scenario and not generally
await client.WaitForOutboundHealthAsync();

// Retrieve a key-value-pair-based secret - returns a Dictionary<string, string>
var secrets = await client.GetSecretAsync("mysecretstore", "key-value-pair-secret");
Console.WriteLine($"Got secret keys: {string.Join(", ", secrets.Keys)}");

// Retrieve a single-valued secret - returns a Dictionary<string, string>
// containing a single value with the secret name as the key
var data = await client.GetSecretAsync("mysecretstore", "single-value-secret");
var value = data["single-value-secret"]
Console.WriteLine("Got a secret value, I'm not going to be print it, it's a secret!");
```

{{% /tab %}}

{{< /tabpane >}}

- For a full guide on secrets visit [How-To: Retrieve secrets]({{% ref howto-secrets.md %}}).

### Get Configuration Keys
```csharp
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace LockService
{
    class Program
    {
        [Obsolete("Distributed Lock API is in Alpha, this can be removed once it is stable.")]
        static async Task Main(string[] args)
        {
            const string daprLockName = "lockstore";
            const string fileName = "my_file_name";
            
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddDaprClient();
            });
            var app = builder.Build();
            
            using var scope = app.Services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprClient>();
     
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
            
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddDaprClient();
            });
            var app = builder.Build();
            
            using var scope = app.Services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprClient>();
            
            var response = await client.Unlock(DAPR_LOCK_NAME, "my_file_name", "random_id_abc123"));
            Console.WriteLine(response.status);
        }
    }
}
```

## Sidecar APIs
### Sidecar Health
While the .NET SDK provides a way to poll for the sidecar health, it is not generally recommended that developer
utilize this functionality unless they are explicitly using Dapr to also retrieve secrets or configuration values.

There are two methods available:
- `CheckOutboundHealthAsync` which queries an outbound readiness endpoint in the Dapr Health API {{% ref "health_api#wait-for-specific-health-check-against-outbound-path" %}}
for a successful HTTP status code and reports readiness based on this value.
- `WaitForSidecarAsync` continuously polls `CheckOutboundHealthAsync` until it returns a successful status code.

The "outbound" direction refers to the communication outbound from the Dapr runtime to your application. If your 
application doesn't use actors, secret management, configuration retrieval or workflows, the runtime will not attempt
to create an outbound connection. This means that if your application takes a dependency on `WaitForSidecarAsync`
without using any of these Dapr components, it will indefinitely lock up during startup as the endpoint will never be established.

A future release will remove these methods altogether and perform this as an internal SDK operation, so neither
method should be relied on in general. Reach out in the Discord #dotnet-sdk channel for more clarification as
to whether your scenario may necessitate using this, but in most situations, these methods should not be required.


### Shutdown the sidecar
```csharp
var client = new DaprClientBuilder().Build();
await client.ShutdownSidecarAsync();
```

## Related links
- [.NET SDK examples](https://github.com/dapr/dotnet-sdk/tree/master/examples)
