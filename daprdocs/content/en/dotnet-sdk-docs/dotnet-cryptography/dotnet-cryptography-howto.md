---
type: docs
title: "How to: Create an use Dapr Cryptography in the .NET SDK"
linkTitle: "How to: Use the Cryptography client"
weight: 510100
description: Learn how to create and use the Dapr Cryptography client using the .NET SDK
---

## Prerequisites
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0), or [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost)

## Installation
To get started with the Dapr Cryptography client, install the [Dapr.Cryptography package](https://www.nuget.org/packages/Dapr.Cryptography) from NuGet:
```sh
dotnet add package Dapr.Cryptography
```

A `DaprEncryptionClient` maintains access to networking resources in the form of TCP sockets used to communicate with 
the Dapr sidecar.

### Dependency Injection

The `AddDaprEncryptionClient()` method will register the Dapr client with dependency injection and is the recommended approach
for using this package. This method accepts an optional options delegate for configuring the `DaprEncryptionClient` and a
`ServiceLifetime` argument, allowing you to specify a different lifetime for the registered services instead of the default `Singleton`
value.

The following example assumes all default values are acceptable and is sufficient to register the `DaprEncryptionClient`:

```csharp
services.AddDaprEncryptionClient();
```

The optional configuration delegate is used to configure the `DaprEncryptionClient` by specifying options on the
`DaprEncryptionClientBuilder` as in the following example:
```csharp
services.AddSingleton<DefaultOptionsProvider>();
services.AddDaprEncryptionClient((serviceProvider, clientBuilder) => {
     //Inject a service to source a value from
     var optionsProvider = serviceProvider.GetRequiredService<DefaultOptionsProvider>();
     var standardTimeout = optionsProvider.GetStandardTimeout();
     
     //Configure the value on the client builder
     clientBuilder.UseTimeout(standardTimeout);
});
```

### Manual Instantiation
Rather than using dependency injection, a `DaprEncryptionClient` can also be built using the static client builder.

For best performance, create a single long-lived instance of `DaprEncryptionClient` and provide access to that shared instance throughout
your application. `DaprEncryptionClient` instances are thread-safe and intended to be shared.

Avoid creating a `DaprEncryptionClient` per-operation.

A `DaprEncryptionClient` can be configured by invoking methods on the `DaprEncryptionClientBuilder` class before calling `.Build()`
to create the client. The settings for each `DaprEncryptionClient` are separate and cannot be changed after calling `.Build()`.

```csharp
var daprEncryptionClient = new DaprEncryptionClientBuilder()
    .UseJsonSerializerSettings( ... ) //Configure JSON serializer
    .Build();
```

See the .NET [documentation here]({{< ref dotnet-client >}}) for more information about the options available when configuring the Dapr client via the builder.

## Try it out
Put the Dapr AI .NET SDK to the test. Walk through the samples to see Dapr in action:

| SDK Samples                                                                         | Description |
|-------------------------------------------------------------------------------------| ----------- |
| [SDK samples](https://github.com/dapr/dotnet-sdk/tree/master/examples/Cryptography) | Clone the SDK repo to try out some examples and get started. |
