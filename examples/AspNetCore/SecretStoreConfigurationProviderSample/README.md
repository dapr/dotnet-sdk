# Dapr secret store configuration provider in ASP.NET Core

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Overview

This document describes how to use the Dapr Secret Store Configuration Provider sample to load Secrets into ASP.NET Core Configuration.

To load secrets into configuration call the _AddDaprSecretStore_ extension method with the name of the Secret Store and a list of secrets descriptors or related metadata.

## Using the Dapr Secret Store Configuration Provider ASP.NET Core example

### 1. Use Dapr to run the application 

Use Dapr to run the application:

```shell
dapr run --app-id SecretStoreConfigurationProviderSample --components-path ./components/ -- dotnet run
```

### 2. Test the application

Run the following command in other terminal:

``` shell
curl http://localhost:5000/secret
```

The response should read: "This the way"
