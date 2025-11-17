---
type: docs
title: "Dapr Distributed Lock .NET SDK"
linkTitle: "Distributed Lock"
weight: 61000
description: Get up and running with the Dapr Distributed .NET SDK
---

With the Dapr Distributed Lock package, you can create and remove locks on resources to manage exclusivity across
your distributed applications.

While this capability is implemented in both the `Dapr.Client` and `Dapr.DistributedLock` packages, the approach differs
slightly between them and a future release will see the `Dapr.Client` package be deprecated. It's recommended that new
implementations use the `Dapr.DistributedLock` package. This document will reflect the implementation in the 
`Dapr.DistributedLock` package.

## Lifetime management
A `DaprDistributedLockClient` is a version of the Dapr client that is dedicated to interacting with Dapr's distributed
lock API. It can be registered alongside a `DaprClient` and other Dapr clients without issue.

It maintains access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar runtime.

For best performance, it is recommended that you utilize the dependency injection container mechanisms provided with the 
`Dapr.DistributedLock` package to provide easy access to an injected instance throughout your application. These injected
instances are thread-safe and intended to be used across different types within your application. Registration via
dependency injection can utilize values from an `IConfiguration` or other injected services in a way that's impractical
when creating the client from scratch in each of your classes.

If you do opt to manually create a `DaprDistributedLockClient` instance, it is recommended that you use the `DaprClientBuilder`
to create the client. This will ensure that the client is properly configured to communicate with the Dapr sidecar runtime.`

Avoid creawting a `DaprDistributedLockClient` for each operation.

## Configuring a `DaprDistributedLockClient` via `DaprDistributedLockBuilder`

A `DaprDistributedLockClient` can be configured by invoking methods on the `DaprDistributedLockBuilder` class before calling
`.Build()` to create the client itself. The settings for each `DaprDistributedLockClient` are separate and cannot be changed
after calling `.Build()`.

```csharp
var daprDistributedLockClient = new DaprDistributedLockBuilder()
    .UseDaprApiToken("abc123") // Optionally specify the API token used to authenticate to other Dapr sidecars
    .Build();
```

The `DaprDistributedLockBuilder` contains settings for:

- The HTTP endpoint of the Dapr sidecar
- The gRPC endpoint of the Dapr sidecar
- The `JsonSerializerOptions` object used to configure JSON serialization
- The `GrpcChannelOptions` object used to configure gRPC
- The API token used to authenticate requests to the sidecar
- The factory method used to create the `HttpClient` instance used by the SDK
- The timeout used for the `HttpClient` instance when making requests to the sidecar

The SDK will read the following environment variables to configure the default values:

- `DAPR_HTTP_ENDPOINT`: used to find the HTTP endpoint of the Dapr sidecar, example: `https://dapr-api.mycompany.com`
- `DAPR_GRPC_ENDPOINT`: used to find the gRPC endpoint of the Dapr sidecar, example: `https://dapr-grpc-api.mycompany.com`
- `DAPR_HTTP_PORT`: if `DAPR_HTTP_ENDPOINT` is not set, this is used to find the HTTP local endpoint of the Dapr sidecar
- `DAPR_GRPC_PORT`: if `DAPR_GRPC_ENDPOINT` is not set, this is used to find the gRPC local endpoint of the Dapr sidecar
- `DAPR_API_TOKEN`: used to set the API token

### Configuring gRPC channel options

Dapr's use of `CancellationToken` for cancellation relies on the configuration of the gRPC channel options. If you need
to configure these options yourself, make sure to enable the [ThrowOperationCanceledOnCancellation setting](https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Net.Client.GrpcChannelOptions.html#Grpc_Net_Client_GrpcChannelOptions_ThrowOperationCanceledOnCancellation).

```cs
var daprDistributedLockClient = new DaprDistributedLockBuilder()
    .UseGrpcChannelOptions(new GrpcChannelOptions { ... ThrowOperationCanceledOnCancellation = true })
    .Build();
```

## Using cancellation with `DaprDistributedLockClient`

The APIs on `DaprDistributedLockClient` perform asynchronous operations and accept an optional `CancellationToken` parameter. This
follows a standard .NET practice for cancellable operations. Note that when cancellation occurs, there is no guarantee that
the remote endpoint stops processing the request, only that the client has stopped waiting for completion.

When an operation is cancelled, it will throw an `OperationCancelledException`.

## Configuring `DaprDistributedLockClient` via dependency injection

Using the built-in extension methods for registering the `DaprDistributedLockClient` in a dependency injection container can
provide the benefit of registering the long-lived service a single time, centralize complex configuration and improve
performance by ensuring similarly long-lived resources are re-purposed when possible (e.g. `HttpClient` instances).

There are three overloads available to give the developer the greatest flexibility in configuring the client for their
scenario. Each of these will register the `IHttpClientFactory` on your behalf if not already registered, and configure
the `DaprDistributedLockBuilder` to use it when creating the `HttpClient` instance in order to re-use the same instance as
much as possible and avoid socket exhaustion and other issues.

In the first approach, there's no configuration done by the developer and the `DaprDistributedLockClient` is configured with the
default settings.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprDistributedLock(); //Registers the `DaprDistributedLockClient` to be injected as needed
var app = builder.Build();
```

Sometimes the developer will need to configure the created client using the various configuration options detailed
above. This is done through an overload that passes in the `DaprDistributedLockBuilder` and exposes methods for configuring
the necessary options.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprDistributedLock((_, daprDistributedLockBuilder) => {
   //Set the API token
   daprDistributedLockBuilder.UseDaprApiToken("abc123");
   //Specify a non-standard HTTP endpoint
   daprDistributedLockBuilder.UseHttpEndpoint("http://dapr.my-company.com");
});

var app = builder.Build();
```

Finally, it's possible that the developer may need to retrieve information from another service in order to populate
these configuration values. That value may be provided from a `DaprClient` instance, a vendor-specific SDK or some
local service, but as long as it's also registered in DI, it can be injected into this configuration operation via the
last overload:

```cs
var builder = WebApplication.CreateBuilder(args);

//Register a fictional service that retrieves secrets from somewhere
builder.Services.AddSingleton<SecretService>();

builder.Services.AddDaprDistributedLock((serviceProvider, daprDistributedLockBuilder) => {
    //Retrieve an instance of the `SecretService` from the service provider
    var secretService = serviceProvider.GetRequiredService<SecretService>();
    var daprApiToken = secretService.GetSecret("DaprApiToken").Value;

    //Configure the `DaprDistributedLockBuilder`
    daprDistributedLockBuilder.UseDaprApiToken(daprApiToken);
});

var app = builder.Build();
```

