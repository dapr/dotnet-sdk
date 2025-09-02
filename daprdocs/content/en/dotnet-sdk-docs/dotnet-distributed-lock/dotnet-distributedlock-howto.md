---
type: docs
title: "How to: Create and use Dapr Distributed Lock in the .NET SDK"
linkTitle: "How to: Use the Distributed Lock client"
weight: 61050
description: Learn how to create and use the Dapr Distributed Lock client using the .NET SDK
---

## Prerequisites
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0), or [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost)

## Installation

To get started with the Dapr Distributed lock .NET SDK client, install the [Dapr.Distributed Lock package](https://www.nuget.org/packages/Dapr.DistributedLock) from NuGet:
```sh
dotnet add package Dapr.DistributedLock
```

A `DaprDistributedLockClient` maintains access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar.

### Dependency Injection

The `AddDaprDistributedLock()` method will register the Dapr client ASP.NET Core dependency injection and is the recommended approach
for using this package. This method accepts an optional options delegate for configuring the `DaprDistributedLockClient` and a
`ServiceLifetime` argument, allowing you to specify a different lifetime for the registered services instead of the default `Singleton`
value.

The following example assumes all default values are acceptable and is sufficient to register the `DaprDistributedLockClient`:

```csharp
services.AddDaprDistributedLock();
```

The optional configuration delegate is used to configure the `DaprDistributedLockClient` by specifying options on the
`DaprDistributedLockBuilder` as in the following example:
```csharp
services.AddSingleton<DefaultOptionsProvider>();
services.AddDaprDistributedLock((serviceProvider, clientBuilder) => {
     //Inject a service to source a value from
     var optionsProvider = serviceProvider.GetRequiredService<DefaultOptionsProvider>();
     var standardTimeout = optionsProvider.GetStandardTimeout();
     
     //Configure the value on the client builder
     clientBuilder.UseTimeout(standardTimeout);
});
```

### Manual Instantiation
Rather than using dependency injection, a `DaprDistributedLockClient` can also be built using the static client builder.

For best performance, create a single long-lived instance of `DaprDistributedLockClient` and provide access to that shared instance throughout
your application. `DaprDistributedLockClient` instances are thread-safe and intended to be shared.

Avoid creating a `DaprDistributedLockClient` per-operation.

A `DaprDistributedLockClient` can be configured by invoking methods on the `DaprDistributedLockBuilder` class before calling `.Build()`
to create the client. The settings for each `DaprDistributedLockClient` are separate and cannot be changed after calling `.Build()`.

```csharp
var daprDistributedLockClient = new DaprDistributedLockBuilder()
    .UseJsonSerializerSettings( ... ) //Configure JSON serializer
    .Build();
```

See the .NET [documentation here]({{% ref dotnet-distributed-lock %}}) for more information about the options available 
when configuring the Dapr Distributed Lock client via the builder.

## Try it out
Put the Dapr Distributed Lock .NET SDK to the test. Walk through the samples to see Dapr in action:

| SDK Samples | Description |
| ----------- | ----------- |
| [SDK samples](https://github.com/dapr/dotnet-sdk/tree/master/examples) | Clone the SDK repo to try out some examples and get started. |

## Building Blocks

This part of the .NET SDK allows you to interface with the Distributed Lock API to place and remove locks for managing
resource exclusivity across your distributed applications.
