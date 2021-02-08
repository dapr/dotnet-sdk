---
type: docs
title: "Getting started with the Dapr client .NET SDK"
linkTitle: "Client"
weight: 20000
description: How to get up and running with the Dapr .NET SDK
no_list: true
---

The Dapr client package allows you to interact with other Dapr applications from a .NET application.

## Prerequisites

- [Dapr CLI]({{< ref install-dapr-cli.md >}}) installed
- Initialized [Dapr environment]({{< ref install-dapr-selfhost.md >}})
- [.NET 5.0+](https://dotnet.microsoft.com/download) installed

## Building blocks

The .NET SDK allows you to interface with all of the [Dapr building blocks]({{< ref building-blocks >}}).

### Invoke a service

{{< tabs SDK HTTP>}}

{{% codetab %}}
```csharp
using var client = new DaprClientBuilder().Build();

// Invokes a POST method named "deposit" that takes input of type "Transaction"
var data = new { id = "17", amount = 99m };
var account = await client.InvokeMethodAsync<object, Account>("routing", "deposit", data, cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
```
{{% /codetab %}}

{{% codetab %}}
```csharp
var deposit = new Transaction  { Id = "17", Amount = 99m };
var response = await client.PostAsJsonAsync("/deposit", deposit, cancellationToken);
var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cancellationToken);
Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
```
{{% /codetab %}}

{{< /tabs >}}

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

- For a full list of state operations visit [How-To: Get & save state]({{< ref howto-get-save-state.md >}}).

### Publish messages

```csharp
var client = new DaprClientBuilder().Build();

var eventData = new { Id = "17", Amount = 10m, };
await client.PublishEventAsync(pubsubName, "deposit", eventData, cancellationToken);
Console.WriteLine("Published deposit event!");
```

- For a full list of state operations visit [How-To: Publish & subscribe]({{< ref howto-publish-subscribe.md >}}).
- Visit [Python SDK examples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples/pubsub-simple) for code samples and instructions to try out pub/sub

### Interact with output bindings

```csharp
//TODO
```

- For a full guide on output bindings visit [How-To: Use bindings]({{< ref howto-bindings.md >}}).

### Retrieve secrets

```csharp
//TODO
```

- For a full guide on secrets visit [How-To: Retrieve secrets]({{< ref howto-secrets.md >}}).

## Related links
- [.NET SDK examples](https://github.com/dapr/dotnet-sdk/tree/master/examples)