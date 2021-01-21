# Dapr Secret Store Configuration Provider in ASP.NET Core

## Prerequistes
* [.Net Core SDK 3.1](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)

## Overview

This document describes how to use the Dapr Secret Store Configuration Provider sample to load Secrets into ASP.NET Core Configuration.

To load secrets into configuration call the _AddDaprSecretStore_ extension method with the name of the Secret Store and a list of secrets descriptors or related metadata.

## Using the Dapr Secret Store Configuration Provider ASP.NET Core sample

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