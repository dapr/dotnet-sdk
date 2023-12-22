# Dapr .NET SDK Cryptography example

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/installation)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Running the example

To run the sample locally, run this command in the DaprClient directory:

```sh
dapr run --resources-path ./Components --app-id DaprClient -- dotnet run <zero-indexed sample number>
```

Running the following command will output a list of the samples included:

```sh
dapr run --resources-path ./Components --app-id DaprClient -- dotnet run
```

Press Ctrl+C to exit, and then run the command again and provide a sample number to run the samples. 

For example, run this command to run the first sample from the list produced earlier (the 0th example):

```sh
dapr run --resources-path ./Components --app-id DaprClient -- dotnet run 0
```

## Encryption/Decryption with strings
See [EncryptStringExample.cs](./EncryptStringExample.cs) for an example of using `DaprClient` for basic string-based encryption and decryption operations as performed against UTF-8 encoded byte arrays.