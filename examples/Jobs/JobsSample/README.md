# Dapr Jobs Sample

This sample demonstrates scheduling a job with the Dapr Jobs SDK and receiving
trigger callbacks via a handler delegate.

## How it works

`AddDaprJobsClient()` registers both the HTTP callback endpoint (`POST /job/{jobName}`)
and the gRPC `AppCallbackAlpha.OnJobEventAlpha1` service. The Dapr sidecar invokes
whichever protocol it is configured for; the other handler stays idle.

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)

## Kestrel configuration for gRPC

gRPC over plaintext (non-TLS) requires Kestrel to be configured for `HttpProtocols.Http2`.
This is because HTTP/2 negotiation needs either TLS with ALPN or explicit HTTP/2-only
endpoint configuration. `Http1AndHttp2` does **not** support HTTP/2 over plaintext.

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http2);
});
```

For the default HTTP mode (`--app-protocol http`), no Kestrel configuration is needed.

> **Note:** The sample already includes this Kestrel configuration, so it works with both
> gRPC and HTTP sidecar configurations. If you only use HTTP mode, you can remove it.

## Run with gRPC (recommended)

```sh
dapr run --app-id jobs-sample --app-port 5140 --app-protocol grpc --dapr-grpc-port 50001 -- dotnet run
```

## Run with HTTP (default)

```sh
dapr run --app-id jobs-sample --app-port 5140 --dapr-grpc-port 50001 -- dotnet run
```

## Expected output

The application schedules a job that fires every 2 seconds for 10 repetitions.
Each trigger logs the job name and payload:

```
info: Scheduling one-time job 'myJob' to execute 10 seconds from now
info: Scheduled one-time job 'myJob'
info: Received trigger invocation for job 'myJob'
info: Received invocation for the job 'myJob' with payload 'This is a test'
```
