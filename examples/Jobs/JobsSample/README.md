# Dapr Jobs Sample

This sample demonstrates scheduling a job with the Dapr Jobs SDK and receiving
trigger callbacks via a handler delegate.

## How it works

`AddDaprJobsClient()` registers both the HTTP callback endpoint (`POST /job/{jobName}`)
and the gRPC `AppCallbackAlpha.OnJobEventAlpha1` service, and configures Kestrel for
HTTP/1 + HTTP/2. The Dapr sidecar invokes whichever protocol it is configured for;
the other handler stays idle. **No application code changes are needed to switch
between HTTP and gRPC** — only the `dapr run` command changes.

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)

## Run with gRPC (recommended)

Starting the sidecar with `--app-protocol grpc` makes the Dapr runtime deliver job
triggers over gRPC:

```sh
dapr run --app-id jobs-sample --app-port 5140 --app-protocol grpc --dapr-grpc-port 50001 -- dotnet run
```

## Run with HTTP (default)

Starting the sidecar without `--app-protocol` defaults to HTTP:

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
