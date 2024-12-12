---
type: docs
title: "DaprClient usage"
linkTitle: "DaprClient usage"
weight: 100000
description: Essential tips and advice for using DaprClient
---

## Lifetime management

A `DaprClient` holds access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar. `DaprClient` implements `IDisposable` to support eager cleanup of resources.

### Dependency Injection

The `AddDaprClient()` method will register the Dapr client with ASP.NET Core dependency injection. This method accepts an optional
options delegate for configuring the `DaprClient` and an `ServiceLifetime` argument, allowing you to specify a different lifetime
for the registered resources instead of the default `Singleton` value.

The following example assumes all default values are acceptable and is sufficient to register the `DaprClient`.

```csharp
services.AddDaprClient();
```

The optional configuration delegates are used to configure `DaprClient` by specifying options on the provided `DaprClientBuilder`
as in the following example:

```csharp
services.AddDaprClient(daprBuilder => {
    daprBuilder.UseJsonSerializerOptions(new JsonSerializerOptions {
            WriteIndented = true,
            MaxDepth = 8
        });
    daprBuilder.UseTimeout(TimeSpan.FromSeconds(30));
});
```

The another optional configuration delegate overload provides access to both the `DaprClientBuilder` as well as an `IServiceProvider`
allowing for more advanced configurations that may require injecting services from the dependency injection container.

```csharp
services.AddSingleton<SampleService>();
services.AddDaprClient((serviceProvider, daprBuilder) => {
    var sampleService = serviceProvider.GetRequiredService<SampleService>();
    var timeoutValue = sampleService.TimeoutOptions;
    
    daprBuilder.UseTimeout(timeoutValue);
});
```

### Manual Instantiation

Rather than using dependency injection, a `DaprClient` can also be built using the static client builder.

For best performance, create a single long-lived instance of `DaprClient` and provide access to that shared instance throughout your application. `DaprClient` instances are thread-safe and intended to be shared.

Avoid creating a `DaprClient` per-operation and disposing it when the operation is complete. 

## Configuring DaprClient

A `DaprClient` can be configured by invoking methods on `DaprClientBuilder` class before calling `.Build()` to create the client. The settings for each `DaprClient` object are separate and cannot be changed after calling `.Build()`.

```C#
var daprClient = new DaprClientBuilder()
    .UseJsonSerializerSettings( ... ) // Configure JSON serializer
    .Build();
```

By default, the `DaprClientBuilder` will prioritize the following locations, in the following order, to source the configuration
values:

- The value provided to a method on the `DaprClientBuilder` (e.g. `UseTimeout(TimeSpan.FromSeconds(30))`)
- The value pulled from an optionally injected `IConfiguration` matching the name expected in the associated environment variable
- The value pulled from the associated environment variable
- Default values

### Configuring on `DaprClientBuilder`

The `DaprClientBuilder` contains the following methods to set configuration options:

- `UseHttpEndpoint(string)`: The HTTP endpoint of the Dapr sidecar
- `UseGrpcEndpoint(string)`: Sets the gRPC endpoint of the Dapr sidecar
- `UseGrpcChannelOptions(GrpcChannelOptions)`: Sets the gRPC channel options used to connect to the Dapr sidecar
- `UseHttpClientFactory(IHttpClientFactory)`: Configures the DaprClient to use a registered `IHttpClientFactory` when building `HttpClient` instances
- `UseJsonSerializationOptions(JsonSerializerOptions)`: Used to configure JSON serialization
- `UseDaprApiToken(string)`: Adds the provided token to every request to authenticate to the Dapr sidecar
- `UseTimeout(TimeSpan)`: Specifies a timeout value used by the `HttpClient` when communicating with the Dapr sidecar

### Configuring From `IConfiguration`
Rather than rely on sourcing configuration values directly from environment variables or because the values are sourced 
from dependency injected services, another options is to make these values available on `IConfiguration`.

For example, you might be registering your application in a multi-tenant environment and need to prefix the environment 
variables used. The following example shows how these values can be sourced from the environment variables to your
`IConfiguration` when their keys are prefixed with `test_`;

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables("test_"); //Retrieves all environment variables that start with "test_" and removes the prefix when sourced from IConfiguration
builder.Services.AddDaprClient();
```

### Configuring From Environment Variables

The SDK will read the following environment variables to configure the default values:

- `DAPR_HTTP_ENDPOINT`: used to find the HTTP endpoint of the Dapr sidecar, example: `https://dapr-api.mycompany.com`
- `DAPR_GRPC_ENDPOINT`: used to find the gRPC endpoint of the Dapr sidecar, example: `https://dapr-grpc-api.mycompany.com`
- `DAPR_HTTP_PORT`: if `DAPR_HTTP_ENDPOINT` is not set, this is used to find the HTTP local endpoint of the Dapr sidecar
- `DAPR_GRPC_PORT`: if `DAPR_GRPC_ENDPOINT` is not set, this is used to find the gRPC local endpoint of the Dapr sidecar
- `DAPR_API_TOKEN`: used to set the API Token

{{% alert title="Note" color="primary" %}}
If both `DAPR_HTTP_ENDPOINT` and `DAPR_HTTP_PORT` are specified, the port value from `DAPR_HTTP_PORT` will be ignored in favor of the port
implicitly or explicitly defined on `DAPR_HTTP_ENDPOINT`. The same is true of both `DAPR_GRPC_ENDPOINT` and `DAPR_GRPC_PORT`.
{{% /alert %}}

### Configuring gRPC channel options

Dapr's use of `CancellationToken` for cancellation relies on the configuration of the gRPC channel options and this is enabled by default. If you need to configure these options yourself, make sure to enable the [ThrowOperationCanceledOnCancellation setting](https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Net.Client.GrpcChannelOptions.html#Grpc_Net_Client_GrpcChannelOptions_ThrowOperationCanceledOnCancellation).

```C#
var daprClient = new DaprClientBuilder()
    .UseGrpcChannelOptions(new GrpcChannelOptions { ... ThrowOperationCanceledOnCancellation = true })
    .Build();
```

## Using cancellation with DaprClient

The APIs on DaprClient that perform asynchronous operations accept an optional `CancellationToken` parameter. This follows a standard .NET idiom for cancellable operations. Note that when cancellation occurs, there is no guarantee that the remote endpoint stops processing the request, only that the client has stopped waiting for completion.

When an operation is cancelled, it will throw an `OperationCancelledException`. 

## Understanding DaprClient JSON serialization

Many methods on `DaprClient` perform JSON serialization using the `System.Text.Json` serializer. Methods that accept an application data type as an argument will JSON serialize it, unless the documentation clearly states otherwise.

It is worth reading the [System.Text.Json documentation](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview) if you have advanced requirements. The Dapr .NET SDK provides no unique serialization behavior or customizations - it relies on the underlying serializer to convert data to and from the application's .NET types.

`DaprClient` is configured to use a serializer options object configured from [JsonSerializerDefaults.Web](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializerdefaults?view=net-5.0). This means that `DaprClient` will use `camelCase` for property names, allow reading quoted numbers (`"10.99"`), and will bind properties case-insensitively. These are the same settings used with ASP.NET Core and the `System.Text.Json.Http` APIs, and are designed to follow interoperable web conventions.

`System.Text.Json` as of .NET 5.0 does not have good support for all of F# language features built-in. If you are using F# you may want to use one of the converter packages that add support for F#'s features such as [FSharp.SystemTextJson](https://github.com/Tarmil/FSharp.SystemTextJson).

### Simple guidance for JSON serialization

Your experience using JSON serialization and `DaprClient` will be smooth if you use a feature set that maps to JSON's type system. These are general guidelines that will simplify your code where they can be applied.

- Avoid inheritance and polymorphism
- Do not attempt to serialize data with cyclic references
- Do not put complex or expensive logic in constructors or property accessors
- Use .NET types that map cleanly to JSON types (numeric types, strings, `DateTime`)
- Create your own classes for top-level messages, events, or state values so you can add properties in the future
- Design types with `get`/`set` properties OR use the [supported pattern](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-immutability?pivots=dotnet-5-0) for immutable types with JSON

### Polymorphism and serialization

The `System.Text.Json` serializer used by `DaprClient` uses the declared type of values when performing serialization.

This section will use `DaprClient.SaveStateAsync<TValue>(...)` in examples, but the advice is applicable to any Dapr building block exposed by the SDK.

```C#
public class Widget
{
    public string Color { get; set; }
}
...

// Storing a Widget value as JSON in the state store
widget widget = new Widget() { Color = "Green", };
await client.SaveStateAsync("mystatestore", "mykey", widget);
```

In the example above, the type parameter `TValue` has its type argument inferred from the type of the `widget` variable. This is important because the `System.Text.Json` serializer will perform serialization based on the *declared type* of the value. The result is that the JSON value `{ "color": "Green" }` will be stored.

Consider what happens when you try to use derived type of `Widget`:

```C#
public class Widget
{
    public string Color { get; set; }
}

public class SuperWidget : Widget
{
    public bool HasSelfCleaningFeature { get; set; }
}
...

// Storing a SuperWidget value as JSON in the state store
Widget widget = new SuperWidget() { Color = "Green", HasSelfCleaningFeature = true, };
await client.SaveStateAsync("mystatestore", "mykey", widget);
```

In this example we're using a `SuperWidget` but the variable's declared type is `Widget`. Since the JSON serializer's behavior is determined by the declared type, it only sees a simple `Widget` and will save the value `{ "color": "Green" }` instead of `{ "color": "Green", "hasSelfCleaningFeature": true }`.

If you want the properties of `SuperWidget` to be serialized, then the best option is to override the type argument with `object`. This will cause the serializer to include all data as it knows nothing about the type.

```C#
Widget widget = new SuperWidget() { Color = "Green", HasSelfCleaningFeature = true, };
await client.SaveStateAsync<object>("mystatestore", "mykey", widget);
```

## Error handling

Methods on `DaprClient` will throw `DaprException` or a subclass when a failure is encountered. 

```C#
try
{
    var widget = new Widget() { Color = "Green", };
    await client.SaveStateAsync("mystatestore", "mykey", widget);
}
catch (DaprException ex)
{
    // handle the exception, log, retry, etc.
}
```

The most common cases of failure will be related to:

- Incorrect configuration of Dapr component
- Transient failures such as a networking problem
- Invalid data, such as a failure to deserialize JSON

In any of these cases you can examine more exception details through the `.InnerException` property.
