---
type: docs
title: "DaprJobsClient usage"
linkTitle: "DaprJobsClient usage"
weight: 59000
description: Essential tips and advice for using DaprJobsClient
---

## Lifetime management

A `DaprJobsClient` is a version of the Dapr client that is dedicated to interacting with the Dapr Jobs API. It can be 
registered alongside a `DaprClient` and other Dapr clients without issue.

It maintains access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar and 
implements `IDisposable` to support the eager cleanup of resources.

For best performance, create a single long-lived instance of `DaprJobsClient` and provide access to that shared instance 
throughout your application. `DaprJobsClient` instances are thread-safe and intended to be shared.

This can be aided by utilizing the dependency injection functionality. The registration method supports registration using
as a singleton, a scoped instance or as transient (meaning it's recreated every time it's injected), but also enables
registration to utilize values from an `IConfiguration` or other injected service in a way that's impractical when
creating the client from scratch in each of your classes.

Avoid creating a `DaprJobsClient` for each operation and disposing it when the operation is complete.

## Configuring DaprJobsClient via the DaprJobsClientBuilder

A `DaprJobsClient` can be configured by invoking methods on the `DaprJobsClientBuilder` class before calling `.Build()` 
to create the client itself. The settings for each `DaprJobsClient` are separate
and cannot be changed after calling `.Build()`.

```cs
var daprJobsClient = new DaprJobsClientBuilder()
    .UseDaprApiToken("abc123") // Specify the API token used to authenticate to other Dapr sidecars
    .Build();
```

The `DaprJobsClientBuilder` contains settings for:

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
var daprJobsClient = new DaprJobsClientBuilder()
    .UseGrpcChannelOptions(new GrpcChannelOptions { ... ThrowOperationCanceledOnCancellation = true })
    .Build();
```

## Using cancellation with `DaprJobsClient`

The APIs on `DaprJobsClient` perform asynchronous operations and accept an optional `CancellationToken` parameter. This 
follows a standard .NET practice for cancellable operations. Note that when cancellation occurs, there is no guarantee that 
the remote endpoint stops processing the request, only that the client has stopped waiting for completion.

When an operation is cancelled, it will throw an `OperationCancelledException`. 

## Configuring `DaprJobsClient` via dependency injection

Using the built-in extension methods for registering the `DaprJobsClient` in a dependency injection container can 
provide the benefit of registering the long-lived service a single time, centralize complex configuration and improve 
performance by ensuring similarly long-lived resources are re-purposed when possible (e.g. `HttpClient` instances).

There are three overloads available to give the developer the greatest flexibility in configuring the client for their 
scenario. Each of these will register the `IHttpClientFactory` on your behalf if not already registered, and configure 
the `DaprJobsClientBuilder` to use it when creating the `HttpClient` instance in order to re-use the same instance as 
much as possible and avoid socket exhaustion and other issues.

In the first approach, there's no configuration done by the developer and the `DaprJobsClient` is configured with the 
default settings.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient(); //Registers the `DaprJobsClient` to be injected as needed
var app = builder.Build();
```

Sometimes the developer will need to configure the created client using the various configuration options detailed 
above. This is done through an overload that passes in the `DaprJobsClientBuiler` and exposes methods for configuring 
the necessary options.

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient((_, daprJobsClientBuilder) => {
   //Set the API token
   daprJobsClientBuilder.UseDaprApiToken("abc123");
   //Specify a non-standard HTTP endpoint
   daprJobsClientBuilder.UseHttpEndpoint("http://dapr.my-company.com");
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

builder.Services.AddDaprJobsClient((serviceProvider, daprJobsClientBuilder) => {
    //Retrieve an instance of the `SecretService` from the service provider
    var secretService = serviceProvider.GetRequiredService<SecretService>();
    var daprApiToken = secretService.GetSecret("DaprApiToken").Value;

    //Configure the `DaprJobsClientBuilder`
    daprJobsClientBuilder.UseDaprApiToken(daprApiToken);
});

var app = builder.Build();
```

## Understanding payload serialization on DaprJobsClient

While there are many methods on the `DaprClient` that automatically serialize and deserialize data using the 
`System.Text.Json` serializer, this SDK takes a different philosophy. Instead, the relevant methods accept an optional 
payload of `ReadOnlyMemory<byte>` meaning that serialization is an exercise left to the developer and is not 
generally handled by the SDK.

That said, there are some helper extension methods available for each of the scheduling methods. If you know that you 
want to use a type that's JSON-serializable, you can use the `Schedule*WithPayloadAsync` method for each scheduling 
type that accepts an `object` as a payload and an optional `JsonSerializerOptions` to use when serializing the value. 
This will convert the value to UTF-8 encoded bytes for you as a convenience. Here's an example of what this might 
look like when scheduling a Cron expression:

```cs
public sealed record Doodad (string Name, int Value);

//...
var doodad = new Doodad("Thing", 100);
await daprJobsClient.ScheduleCronJobWithPayloadAsync("myJob", "5 * * * *", doodad);
```

In the same vein, if you have a plain string value, you can use an overload of the same method to serialize a 
string-typed payload and the JSON serialization step will be skipped and it'll only be encoded to an array of 
UTF-8 encoded bytes. Here's an example of what this might look like when scheduling a one-time job:

```cs
var now = DateTime.UtcNow;
var oneWeekFromNow = now.AddDays(7);
await daprJobsClient.ScheduleOneTimeJobWithPayloadAsync("myOtherJob", oneWeekFromNow, "This is a test!");
```

The delegate handling the job invocation expects at least two arguments to be present:
- A `string` that is populated with the `jobName`, providing the name of the invoked job
- A `ReadOnlyMemory<byte>` that is populated with the bytes originally provided during the job registration.

Because the payload is stored as a `ReadOnlyMemory<byte>`, the developer has the freedom to serialize and deserialize 
as they wish, but there are again two helper extensions included that can deserialize this to either a JSON-compatible 
type or a string. Both methods assume that the developer encoded the originally scheduled job (perhaps using the 
helper serialization methods) as these methods will not force the bytes to represent something they're not.

To deserialize the bytes to a string, the following helper method can be used:
```cs
var payloadAsString = Encoding.UTF8.GetString(jobPayload.Span); //If successful, returns a string with the value
```

## Error handling

Methods on `DaprJobsClient` will throw a `DaprJobsServiceException` if an issue is encountered between the SDK 
and the Jobs API service running on the Dapr sidecar. If a failure is encountered because of a poorly formatted 
request made to the Jobs API service through this SDK, a `DaprMalformedJobException` will be thrown. In case of 
illegal argument values, the appropriate standard exception will be thrown (e.g. `ArgumentOutOfRangeException` 
or `ArgumentNullException`) with the name of the offending argument. And for anything else, a `DaprException` 
will be thrown.

The most common cases of failure will be related to:

- Incorrect argument formatting while engaging with the Jobs API
- Transient failures such as a networking problem
- Invalid data, such as a failure to deserialize a value into a type it wasn't originally serialized from

In any of these cases, you can examine more exception details through the `.InnerException` property.
