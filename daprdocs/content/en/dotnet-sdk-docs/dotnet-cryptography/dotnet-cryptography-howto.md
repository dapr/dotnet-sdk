---
type: docs
title: "How to: Author and manage Dapr Cryptography operations in the .NET SDK"
linkTitle: "How to: Author & manage cryptography operations"
weight: 71000
description: Learn how to author and manage Dapr Cryptography operations using the .NET SDK
---

Let's encrypt some data and subsequently decrypt this information using the Cryptography capabilities of
the Dapr .NET SDK. We'll use the [simple example provided here](), for the following demonstration and walk through
it as an explainer of how you can encrypt and decrypt arbitrary byte arrays or streams of data. In this guide, you will:

- Deploy a .NET Web API application ([CryptographSample]())
- Utilize the Dapr .NET Cryptography SDK to encrypt and decrypt a payload

In the .NET example project:
- The main [`Program.cs`]() file comprises the entirety of this demonstration.

## Prerequisites
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost)
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) or [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) installed
- [Dapr.Cryptography](https://www.nuget.org/packages/Dapr.Cryptography) NuGet package installed to your project

## Set up the environment
Clone the [.NET SDK repo](https://github.com/dapr/dotnet-sdk).

```sh
git clone https://github.com/dapr/dotnet-sdk.git
```

From the .NET SDK root directory, navigate to the Dapr Cryptography example.

```sh
cd examples/Cryptography
```

## Run the application locally

To run the Dapr application, you need to start the .NET program and a Dapr sidecar. Navigate to the `CryptographySample` directory.

```sh
cd CryptographySample
```

We'll run a command that starts both the Dapr sidecar and the .NET program at the same time.

```sh
dapr run --app-id cryptoapp --dapr-grpc-port 4001 --dapr-http-port 3500 -- dotnet run
```

> Dapr listens for HTTP requests at `http://localhost:3500` and internal Jobs gRPC requests at `http://localhost:4001`.

## Register the Dapr Encryption client with dependency injection
The Dapr Cryptography SDK provides an extension method to simplify the registration of the Dapr encryption client. 
Before completing the dependency injection registration in `Program.cs`, add the following line:

```cs
var builder = WebApplication.CreateBuilder(args);

//Add anywhere between these two lines
builder.Services.AddDaprEncryptionClient();

var app = builder.Build();
```

It's possible that you may want to provide some configuration options to the Dapr encryption client that
should be present with each call to the sidecar such as a Dapr API token, or you want to use a non-standard
HTTP or gRPC endpoint. This is possible through use of an overload of the registration method that allows 
configuration of a `DaprEncryptionClientBuilder` instance:

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprEncryptionClient((_, daprEncryptionClientBuilder) =>
{
    daprEncryptionClientBuilder.UseDaprApiToken("abc123");
    daprEncryptionClientBuilder.UseHttpEndpoint("http://localhost:8512"); //Non-standard sidecar HTTP endpoint
});

var app = builder.Build();
```

Still, it's possible that whatever values you wish to inject need to be retrieved from some other source, itself 
registered as a dependency. There's one more overload you can use to inject an `IServiceProvider` into the 
configuration action method. In the following example, we register a fictional singleton that can retrieve 
secrets from somewhere and pass it into the configuration method for `AddDaprEncryptionClient` so
we can retrieve our Dapr API token from somewhere else for registration here:

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SecretRetriever>();
builder.Services.AddDaprJobsClient((serviceProvider, daprEncryptionClientBuilder) =>
{
    var secretRetriever = serviceProvider.GetRequiredService<SecretRetriever>();
    var daprApiToken = secretRetriever.GetSecret("DaprApiToken").Value;
    daprJobsClientBuilder.UseDaprApiToken(daprApiToken);

    daprJobsClientBuilder.UseHttpEndpoint("http://localhost:8512");
});

var app = builder.Build();
```

## Use the Dapr Encryption client using IConfiguration
It's possible to configure the Dapr Encryption client using the values in your registered `IConfiguration` as well without
explicitly specifying each of the value overrides using the `DaprEncryptionlientBuilder` as demonstrated in the previous
section. Rather, by populating an `IConfiguration` made available through dependency injection the `AddDaprEncryptionClient()`
registration will automatically use these values over their respective defaults.

Start by populating the values in your configuration. This can be done in several different ways as demonstrated below.

### Configuration via `ConfigurationBuilder`
Application settings can be configured without using a configuration source and by instead populating the value in-memory
using a `ConfigurationBuilder` instance:

```csharp
var builder = WebApplication.CreateBuilder();

//Create the configuration
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string> {
            { "DAPR_HTTP_ENDPOINT", "http://localhost:54321" },
            { "DAPR_API_TOKEN", "abc123" }
        })
    .Build();

builder.Configuration.AddConfiguration(configuration);
builder.Services.AddDaprEncryptionClient(); //This will automatically populate the HTTP endpoint and API token values from the IConfiguration
```

### Configuration via Environment Variables
Application settings can be accessed from environment variables available to your application.

The following environment variables will be used to populate both the HTTP endpoint and API token used to register the
Dapr Jobs client.

| Key | Value |
| --- | --- |
| DAPR_HTTP_ENDPOINT | http://localhost:54321 |
| DAPR_API_TOKEN | abc123 |

```csharp
var builder = WebApplication.CreateBuilder();

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddDaprEncryptionClient();
```

The Dapr Encryption client will be configured to use both the HTTP endpoint `http://localhost:54321` and populate all outbound
requests with the API token header `abc123`.

### Configuration via prefixed Environment Variables

However, in shared-host scenarios where there are multiple applications all running on the same machine without using
containers or in development environments, it's not uncommon to prefix environment variables. The following example
assumes that both the HTTP endpoint and the API token will be pulled from environment variables prefixed with the
value "myapp_". The two environment variables used in this scenario are as follows:

| Key | Value |
| --- | --- |
| myapp_DAPR_HTTP_ENDPOINT | http://localhost:54321 |
| myapp_DAPR_API_TOKEN | abc123 |

These environment variables will be loaded into the registered configuration in the following example and made available
without the prefix attached.

```csharp
var builder = WebApplication.CreateBuilder();

builder.Configuration.AddEnvironmentVariables(prefix: "myapp_");
builder.Services.AddDaprEncryptionClient();
```

The Dapr Jobs client will be configured to use both the HTTP endpoint `http://localhost:54321` and populate all outbound
requests with the API token header `abc123`.

## Use the Dapr Encryption client without relying on dependency injection
While the use of dependency injection simplifies the use of complex types in .NET and makes it easier to
deal with complicated configurations, you're not required to register the `DaprEncryptionClient` in this way. Rather, 
you can also elect to create an instance of it from a `DaprEncryptionClientBuilder` instance as demonstrated below:

```cs

public class MySampleClass
{
    public void DoSomething()
    {
        var daprEncryptionClientBuilder = new DaprEncryptionClientBuilder();
        var daprEncryptionClient = daprEncryptionClientBuilder.Build();

        //Do something with the `daprEncryptionClient`
    }
}
```

## Encrypt a byte-array payload


## Encrypt a stream-based payload


## Decrypt a payload from a byte array


## Decrypt a stream-based payload

