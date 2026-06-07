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

## Metrics Tracked

The CI pipeline captures and publishes the following metrics on every tagged
release build (RC and GA):

| Metric | Source | Unit |
|--------|--------|------|
| **Latency** | BenchmarkDotNet (mean execution time) | ns / μs / ms |
| **Memory allocations** | BenchmarkDotNet `[MemoryDiagnoser]` | bytes allocated per operation |
| **Package size** | `dotnet pack` output measurement | KB per `.nupkg` |

All metrics are tracked historically and displayed on an interactive
[GitHub Pages dashboard](../../gh-pages) powered by
[benchmark-action/github-action-benchmark](https://github.com/benchmark-action/github-action-benchmark).

### How historical comparison works

1. **On every tagged release build (RC or GA)**: BenchmarkDotNet results (JSON)
   and package sizes are fed to `benchmark-action/github-action-benchmark`.
2. The action commits data points to the `gh-pages` branch under
   `benchmarks/<framework>/` and `package-sizes/`.
3. GitHub Pages serves an interactive chart showing trends over time — each
   data point is a release (or weekly schedule snapshot between releases).
4. **Alert threshold**: If latency regresses by >150% or package size grows by
   >120%, the action posts a comment on the commit (but does **not** fail the
   workflow).

### Viewing the dashboard

Once the workflow has run at least once on a tagged release, the dashboard is
available at:

```
https://dapr.github.io/dotnet-sdk/
```

Each framework (`net8.0`, `net9.0`, `net10.0`) and package sizes have their own
chart panel.

## Adding a New Benchmark

1. Create a new class in the appropriate subdirectory (or create a new one).
2. For benchmarks that need a Dapr sidecar, extend `DaprBenchmarkBase` and use
   `SetupEnvironmentAsync(...)` in `[GlobalSetup]` plus
   `TeardownEnvironmentAsync()` in `[GlobalCleanup]`.
3. For benchmarks that only need a standalone harness (no app), follow the
   pattern in `PubSubBenchmarks`.
4. Decorate with `[MemoryDiagnoser]` for allocation tracking.

## CI Integration

Benchmarks run in the **benchmarks** GitHub Actions workflow on:
- Tag pushes matching `v*` or `v*-rc*` (GA and release candidate builds)
- Published GitHub releases
- Weekly schedule (Sundays at 04:00 UTC) for baseline drift detection
- Manual `workflow_dispatch`

Benchmarks intentionally do **not** run on every PR or `master` push — the
per-test sidecar startup cost makes that prohibitive, and release-cadence data
is what drives the per-release improvement narrative.

The workflow is **informational only** and does not block releases.

Results are:
- Uploaded as workflow artifacts (90-day retention) for raw data access
- Published to the GitHub Pages dashboard for historical trend visualization
- Summarized in the GitHub Actions job summary (package sizes table)
