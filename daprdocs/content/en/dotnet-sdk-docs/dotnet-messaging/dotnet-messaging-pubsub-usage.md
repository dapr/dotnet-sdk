---
type: docs
title: "DaprPublishSubscribeClient usage"
linkTitle: "DaprPublishSubscribeClient usage"
weight: 69000
description: Essential tips and advice for using DaprPublishSubscribeClient
---

## Lifetime management

A `DaprPublishSubscribeClient` is a version of the Dapr client that is dedicated to interacting with the Dapr Messaging API. 
It can be registered alongside a `DaprClient` and other Dapr clients without issue.

It maintains access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar and implements
`IAsyncDisposable` to support the eager cleanup of resources.

For best performance, create a single long-lived instance of `DaprPublishSubscribeClient` and provide access to that shared
instance throughout your application. `DaprPublishSubscribeClient` instances are thread-safe and intended to be shared. 

This can be aided by utilizing the dependency injection functionality. The registration method supports registration using
as a singleton, a scoped instance or as transient (meaning it's recreated every time it's injected), but also enables
registration to utilize values from an `IConfiguration` or other injected service in a way that's impractical when
creating the client from scratch in each of your classes.

Avoid creating a `DaprPublishSubscribeClient` for each operation and disposing it when the operation is complete. It's
intended that the `DaprPublishSubscribeClient` should only be disposed when you no longer wish to receive events on the
subscription as disposing it will cancel the ongoing receipt of new events.

## Configuring DaprPublishSubscribeClient via the DaprPublishSubscribeClientBuilder
A `DaprPublishSubscribeClient` can be configured by invoking methods on the `DaprPublishSubscribeClientBuilder` class 
before calling `.Build()` to create the client itself. The settings for each `DaprPublishSubscribeClient` are separate
and cannot be changed after calling `.Build()`.

```cs
var daprPubsubClient = new DaprPublishSubscribeClientBuilder()
    .UseDaprApiToken("abc123") // Specify the API token used to authenticate to other Dapr sidecars
    .Build();
```

The `DaprPublishSubscribeClientBuilder` contains settings for:

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
Dapr's use of `CancellationToken` for cancellation relies on the configuration of the gRPC channel options. If you 
need to configure these options yourself, make sure to enable the [ThrowOperationCanceledOnCancellation setting](https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Net.Client.GrpcChannelOptions.html#Grpc_Net_Client_GrpcChannelOptions_ThrowOperationCanceledOnCancellation).

```cs
var daprPubsubClient = new DaprPublishSubscribeClientBuilder()
    .UseGrpcChannelOptions(new GrpcChannelOptions { ... ThrowOperationCanceledOnCancellation = true })
    .Build();
```

## Using cancellation with `DaprPublishSubscribeClient`

The APIs on `DaprPublishSubscribeClient` perform asynchronous operations and accept an optional `CancellationToken` 
parameter. This follows a standard .NET practice for cancellable operations. Note that when cancellation occurs, there is 
no guarantee that the remote endpoint stops processing the request, only that the client has stopped waiting for completion.

When an operation is cancelled, it will throw an `OperationCancelledException`.

## Configuring `DaprPublishSubscribeClient` via dependency injection

Using the built-in extension methods for registering the `DaprPublishSubscribeClient` in a dependency injection container 
can provide the benefit of registering the long-lived service a single time, centralize complex configuration and improve 
performance by ensuring similarly long-lived resources are re-purposed when possible (e.g. `HttpClient` instances).

There are three overloads available to give the developer the greatest flexibility in configuring the client for their 
scenario. Each of these will register the `IHttpClientFactory` on your behalf if not already registered, and configure 
the `DaprPublishSubscribeClientBuilder` to use it when creating the `HttpClient` instance in order to re-use the same 
instance as much as possible and avoid socket exhaustion and other issues.

In the first approach, there's no configuration done by the developer and the `DaprPublishSubscribeClient` is configured with 
the default settings.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.DaprPublishSubscribeClient(); //Registers the `DaprPublishSubscribeClient` to be injected as needed
var app = builder.Build();
```

Sometimes the developer will need to configure the created client using the various configuration options detailed above. This is done through an overload that passes in the `DaprJobsClientBuiler` and exposes methods for configuring the necessary options.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient((_, daprPubSubClientBuilder) => {
   //Set the API token
   daprPubSubClientBuilder.UseDaprApiToken("abc123");
   //Specify a non-standard HTTP endpoint
   daprPubSubClientBuilder.UseHttpEndpoint("http://dapr.my-company.com");
});

var app = builder.Build();
```

Finally, it's possible that the developer may need to retrieve information from another service in order to populate these configuration values. That value may be provided from a `DaprClient` instance, a vendor-specific SDK or some local service, but as long as it's also registered in DI, it can be injected into this configuration operation via the last overload:

```cs
var builder = WebApplication.CreateBuilder(args);

//Register a fictional service that retrieves secrets from somewhere
builder.Services.AddSingleton<SecretService>();

builder.Services.AddDaprPublishSubscribeClient((serviceProvider, daprPubSubClientBuilder) => {
    //Retrieve an instance of the `SecretService` from the service provider
    var secretService = serviceProvider.GetRequiredService<SecretService>();
    var daprApiToken = secretService.GetSecret("DaprApiToken").Value;

    //Configure the `DaprPublishSubscribeClientBuilder`
    daprPubSubClientBuilder.UseDaprApiToken(daprApiToken);
});

var app = builder.Build();
```