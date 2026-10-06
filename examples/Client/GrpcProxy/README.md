# gRPC proxy service invocation

This example calls the `GreeterService` in the [ASP.NET Core gRPC service sample](../../AspNetCore/GrpcServiceSample) through Dapr's gRPC proxy. It uses the generated `Greeter.GreeterClient` and `DaprClient.CreateInvocationInvoker`, rather than the Dapr `AppCallback` invocation API.

The client calls its local sidecar, which routes the request to the `grpcsample` application. `CreateInvocationInvoker` adds the `dapr-app-id` routing metadata. The server exposes a normal gRPC service; it does not need to implement `AppCallback` for this call.

## Prerequisites

- The .NET SDK specified by the repository's `global.json` (currently .NET 10).
- The [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) and an [initialized self-hosted Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/).
- Ports 5050, 3500, 3501, 50001 and 50002 available.

Run all commands below from the repository root. The commands select `net10.0` because these samples target multiple frameworks.

## Run the example

In terminal 1, start the server and its sidecar:

```sh
dapr run --app-id grpcsample --app-port 5050 --app-protocol grpc --dapr-http-port 3500 --dapr-grpc-port 50002 -- dotnet run --project examples/AspNetCore/GrpcServiceSample --framework net10.0 --no-launch-profile
```

Wait until the server is listening on port 5050 and Dapr reports that the application is discovered. Keep this terminal running.

In terminal 2, start a separate caller sidecar and run the client:

```sh
dapr run --app-id grpcproxyclient --dapr-http-port 3501 --dapr-grpc-port 50001 -- dotnet run --project examples/Client/GrpcProxy --framework net10.0 -- Ada
```

Expected application output:

```text
Hello Ada
```

Without the final `-- Ada`, the default greeting is `Hello Dapr`.

The client's target app ID is `grpcsample`, not `grpcproxyclient`. Dapr sets `DAPR_GRPC_PORT` for the child process; the SDK uses it to connect to the caller sidecar on port 50001. Port 5050 belongs to the server application, and port 50002 belongs to the server's sidecar. Do not use either as the client's local Dapr endpoint.

The invoker and generated client are created once and reused. Each call has a ten-second deadline, so an unavailable destination does not leave the client waiting indefinitely. Ctrl+C cancels the call. A gRPC failure propagates as `RpcException`; the application prints its status and detail and exits with code 1. Depending on discovery and connection state, the status may be `Unknown`, `Unavailable` or `DeadlineExceeded`.

The greeting operation does not access a state store or pub/sub component. The existing server's banking endpoints are separate from this example.

Stop the server with Ctrl+C when finished. `dapr run` stops each sidecar when its child application exits.

## Verify

```sh
dotnet build examples/Client/GrpcProxy/GrpcProxy.csproj --framework net10.0
dotnet build examples/AspNetCore/GrpcServiceSample/GrpcServiceSample.csproj --framework net10.0
dotnet test examples/Client/GrpcProxy.Tests/GrpcProxy.Tests.csproj --framework net10.0
```

The automated tests simulate the caller's sidecar at the HTTP/2 message-handler boundary. They check the generated RPC path, destination app ID, protobuf request and response, a timeout bounded to ten seconds, propagation of an unavailable response, and cancellation of an in-flight request. They do not verify discovery or forwarding in the Dapr runtime; run the two-terminal example to verify those.

If the example fails, check that terminal 1 is still running, `--app-protocol grpc` is set, and the target app ID matches `grpcsample`. A port conflict requires changing the matching CLI flag; the SDK receives the caller's chosen gRPC port through the environment.

See [Dapr's gRPC service invocation documentation](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/howto-invoke-services-grpc/) for more details.
