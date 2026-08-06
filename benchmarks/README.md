# Dapr Actors Benchmarks

This directory contains benchmarks for comparing the legacy `Dapr.Actors` stack with `Dapr.Actors.Next`.

## BenchmarkDotNet comparison

Run the in-process comparison benchmarks with:

```powershell
dotnet run -c Release --project benchmarks/Dapr.Actors.Benchmarks -- --filter * --join
```

To run only the .NET 10 `Dapr.Actors` vs `Dapr.Actors.Next` comparison benchmarks without Native AOT:

```powershell
dotnet run -c Release --project benchmarks/Dapr.Actors.Benchmarks -- --filter "*.StartupAndRegistrationBenchmarks.*" "*.ProxyCreationBenchmarks.*" "*.InvocationBenchmarks.*" --join
```

The comparison project covers:

- service-provider construction, actor registration, and first runtime dispatch
- proxy/client creation
- warm actor invocation
- cold actor activation plus invocation
- parallel fan-out across actor IDs
- Actors Next .NET 10 JIT vs .NET 10 Native AOT invocation comparison

These benchmarks intentionally avoid the Dapr sidecar so they measure SDK/runtime overhead rather than network and sidecar behavior.

To run only the Actors Next JIT vs Native AOT comparison:

```powershell
dotnet run -c Release --project benchmarks/Dapr.Actors.Benchmarks -- --filter *ActorsNextAotInvocationBenchmarks* --join
```

## Native AOT check for Actors Next

Run the .NET 10 Native AOT benchmark with:

```powershell
dotnet publish -c Release benchmarks/Dapr.Actors.Next.AotBenchmarks
.\bin\Release\benchmarks\Dapr.Actors.Next.AotBenchmarks\net10.0\win-x64\publish\Dapr.Actors.Next.AotBenchmarks.exe
```

The AOT project only references `Dapr.Actors.Next` components and uses generated actor registration/proxy/dispatcher code. It reports startup, first-call, warm-call, cold-activation, and parallel fan-out timings from the published native executable.
