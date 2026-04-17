# Dapr .NET SDK Benchmarks

This project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) together with
[Dapr.Testcontainers](../../src/Dapr.Testcontainers/) to measure the real-world
performance of the Dapr .NET SDK against a live Dapr sidecar running in Docker.

## Prerequisites

- Docker must be running (Testcontainers spins up the Dapr sidecar, placement,
  scheduler, Redis, and RabbitMQ automatically).
- .NET 8.0, 9.0, or 10.0 SDK installed.

## Running Benchmarks

### Run all benchmarks

```bash
dotnet run -c Release --project test/Dapr.Benchmarks -- --filter '*'
```

### Run a specific benchmark class

```bash
# State management only
dotnet run -c Release --project test/Dapr.Benchmarks -- --filter '*StateStore*'

# Pub/sub only
dotnet run -c Release --project test/Dapr.Benchmarks -- --filter '*PubSub*'

# Workflow only
dotnet run -c Release --project test/Dapr.Benchmarks -- --filter '*Workflow*'
```

### Target a specific .NET version

```bash
dotnet run -c Release -f net10.0 --project test/Dapr.Benchmarks -- --filter '*'
```

### Export results

BenchmarkDotNet writes results to `BenchmarkDotNet.Artifacts/` by default.
Override the output directory with `--artifacts`:

```bash
dotnet run -c Release --project test/Dapr.Benchmarks -- --filter '*' --artifacts ./results
```

## Benchmark Categories

| Category | Class | What it measures |
|---|---|---|
| State Management | `StateStoreBenchmarks` | `SaveState`, `GetState`, `DeleteState` round-trips through the Dapr sidecar to a Redis state store with small, medium, and large payloads. |
| Pub/Sub | `PubSubBenchmarks` | `PublishMessages` and end-to-end `PublishAndReceiveMessages` via the Dapr sidecar with a RabbitMQ broker. |
| Workflow | `WorkflowBenchmarks` | `SimpleWorkflow` (single activity) and `FanOutWorkflow` (parallel activities) execution through the Dapr workflow engine. |

## Adding a New Benchmark

1. Create a new class in the appropriate subdirectory (or create a new one).
2. For benchmarks that need a Dapr sidecar, extend `DaprBenchmarkBase` and use
   `SetupEnvironmentAsync(...)` in `[GlobalSetup]` plus
   `TeardownEnvironmentAsync()` in `[GlobalCleanup]`.
3. For benchmarks that only need a standalone harness (no app), follow the
   pattern in `PubSubBenchmarks`.
4. Decorate with `[MemoryDiagnoser]` for allocation tracking.

## CI Integration

Benchmarks run in the **benchmarks** GitHub Actions workflow on pushes to
`master`, on a weekly schedule, and on-demand via `workflow_dispatch`. The
workflow is **informational only** and does not block releases.

Results are uploaded as workflow artifacts for historical comparison.
