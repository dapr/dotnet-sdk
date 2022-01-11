# Example - Get Configuration

This example demonstrates the Configuration APIs in Dapr.
It demonstrates the following APIs:
- **configuration**: Get configuration from statestore

> **Note:** Make sure to use the latest proto bindings

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Store the configuration in configurationstore 
<!-- STEP
name: Set configuration value
expected_stdout_lines:
  - "OK"
timeout_seconds: 20
-->

```bash
docker exec dapr_redis redis-cli SET greeting "hello world||1"
```

<!-- END_STEP -->

## Run the example

Change directory to this folder:
```bash
cd examples/Client/ConfigurationApi
```

To run this example, use the following command:

<!-- STEP
name: Run get configuration example
expected_stdout_lines:
  - "== APP == Querying Configuration with key: greeting"
  - "== APP == Got configuration item:"
  - "== APP == Key: greeting"
  - "== APP == Value: hello world"
  - "== APP == Version: 1"
timeout_seconds: 5
-->

```bash
dapr run --app-id configexample --components-path ./Components -- dotnet run
```
<!-- END_STEP -->

You should be able to see the following output:
```
== APP == Querying Configuration with key: greeting
== APP == Got configuration item:
== APP == Key: greeting
== APP == Value: hello world
== APP == Version: 1
```
