---
type: docs
title: "Dapr .NET SDK"
linkTitle: ".NET"
weight: 1000
description: .NET SDK packages for developing Dapr applications
no_list: true
---

Dapr offers a variety of packages to help with the development of Python applications. Using them you can create Python clients, servers, and virtual actors with Dapr.

## Available packages

- [**Dapr client**]({{< ref dotnet-client.md >}}) for writing .NET applications to interact with the Dapr sidecar and other Dapr applications
- [**Dapr actor**]({{< ref dotnet-actor.md >}}) for creating for creating and interacting with stateful virtual actors in .NET
- [**Extensions**]({{< ref dotnet-sdk-extensions >}}) for adding Dapr capabilities to other .NET frameworks
    - [**ASP.NET**]({{< ref dotnet-aspnet.md >}})
    - [**Azure Functions**]({{< ref dotnet-azurefunctions.md >}})
    - [**Configuration**]({{< ref dotnet-configuration.md >}})

## Try it out

Clone the .NET SDK repo to try out some of the [examples](https://github.com/dapr/dotnet-sdk/tree/master/samples).

```bash
git clone https://github.com/dapr/dotnet-sdk.git
```

## More information

- [NuGet packages](https://www.nuget.org/profiles/dapr.io)
- [Dapr SDK serialization]({{< ref sdk-serialization.md >}})