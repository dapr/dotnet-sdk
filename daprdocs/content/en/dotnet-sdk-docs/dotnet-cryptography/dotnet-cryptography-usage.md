---
type: docs
title: "Dapr Cryptography Client"
linkTitle: "Cryptography client"
weight: 510005
description: Learn how to create Dapr Crytography clients
---

The Dapr Cryptography package allows you to perform encryption and decryption operations provided by the Dapr sidecar.

## Lifetime management
A `DaprEncryptionClient` is a version of the Dapr client that is dedicated to interacting with the Dapr Cryptography API.
It can be registered alongside a `DaprClient` and other Dapr clients without issue.

It maintains access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar.

For best performance, create a single long-lived instance of `DaprEncryptionClient` and provide access to that shared
instance throughout your application. `DaprEncryptionClient` instances are thread-safe and intended to be shared.

This can be aided by utilizing the dependency injection functionality. The registration method supports registration
as a singleton, a scoped instance, or as a transient (meaning it's recreated every time it's injected), but also enables
registration to utilize values from an `IConfiguration` or other injected service in a way that's impractical when creating
the client from scratch in each of your classes.

Avoid creating a `DaprEncryptionClient` for each operation.

## Configuring `DaprEncryptionClient` via `DaprEncryptionClientBuilder`
A `DaprCryptographyClient` can be configured by invoking methods on the `DaprEncryptionClientBuilder` class before calling
`.Build()` to create the client itself. The settings for each `DaprEncryptionClientBuilder` are separate can cannot be
changed after calling `.Build()`.

```cs
var daprEncryptionClient = new DaprEncryptionClientBuilder()
    .UseDaprApiToken("abc123") //Specify the API token used to authenticate to the Dapr sidecar
    .Build();
```

The `DaprEncryptionClientBuilder` contains settings for:
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
var daprEncryptionClient = new DaprEncryptionClientBuilder()
    .UseGrpcChannelOptions(new GrpcChannelOptions { .. ThrowOperationCanceledOnCancellation = true })
    .Build();
```

## Using cancellation with `DaprEncryptionClient`
The APIs on `DaprEncryptionClient` perform asynchronous operations and accept an optional `CancellationToken` parameter. This
follows a standard .NET practice for cancellable operations. Note that when cancellation occurs, there is no guarantee that
the remote endpoint stops processing the request, only that the client has stopped waiting for completion.

When an operation is cancelled, it will throw an `OperationCancelledException`.

## Configuring `DaprEncryptionClient` via dependency injection
Using the built-in extension methods for registering the `DaprEncryptionClient` in a dependency injection container can
provide the benefit of registering the long-lived service a single time, centralize complex configuration and improve
performance by ensuring similarly long-lived resources are re-purposed when possible (e.g. `HttpClient` instances).

There are three overloads available to give the developer the greatest flexibility in configuring the client for their
scenario. Each of these will register the `IHttpClientFactory` on your behalf if not already registered, and configure
the `DaprEncryptionClientBuilder` to use it when creating the `HttpClient` instance in order to re-use the same instance as
much as possible and avoid socket exhaustion and other issues.

In the first approach, there's no configuration done by the developer and the `DaprEncryptionClient` is configured with the
default settings.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprEncryptionClent(); //Registers the `DaprEncryptionClient` to be injected as needed
var app = builder.Build();
```

Sometimes the developer will need to configure the created client using the various configuration options detailed
above. This is done through an overload that passes in the `DaprEncryptionClientBuiler` and exposes methods for configuring
the necessary options.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprEncryptionClient((_, daprEncrpyptionClientBuilder) => {
   //Set the API token
   daprEncryptionClientBuilder.UseDaprApiToken("abc123");
   //Specify a non-standard HTTP endpoint
   daprEncryptionClientBuilder.UseHttpEndpoint("http://dapr.my-company.com");
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

builder.Services.AddDaprEncryptionClient((serviceProvider, daprEncryptionClientBuilder) => {
    //Retrieve an instance of the `SecretService` from the service provider
    var secretService = serviceProvider.GetRequiredService<SecretService>();
    var daprApiToken = secretService.GetSecret("DaprApiToken").Value;

    //Configure the `DaprEncryptionClientBuilder`
    daprEncryptionClientBuilder.UseDaprApiToken(daprApiToken);
});

var app = builder.Build();
```