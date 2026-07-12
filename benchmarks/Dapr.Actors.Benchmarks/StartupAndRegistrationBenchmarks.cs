using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class StartupAndRegistrationBenchmarks
{
    private ServiceProvider provider = null!;

    [Params(ActorPackage.DaprActors, ActorPackage.DaprActorsNext)]
    public ActorPackage Package { get; set; }

    [Params(1, 8)]
    public int ActorTypes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        provider = BenchmarkHostFactory.CreateServiceProvider(Package, ActorTypes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        provider.Dispose();
    }

    [Benchmark]
    public int BuildProviderOnly()
    {
        using var localProvider = BenchmarkHostFactory.CreateServiceProvider(Package, ActorTypes);
        return localProvider.GetHashCode();
    }

    [Benchmark]
    public int ResolveRuntimeFromBuiltProvider()
    {
        return BenchmarkHostFactory.ResolveRuntime(provider, Package).GetHashCode();
    }

    [Benchmark]
    public int BuildProviderAndResolveRuntime()
    {
        using var localProvider = BenchmarkHostFactory.CreateServiceProvider(Package, ActorTypes);
        return BenchmarkHostFactory.ResolveRuntime(localProvider, Package).GetHashCode();
    }

    [Benchmark]
    public int BuildProviderResolveRuntimeAndFirstDispatch()
    {
        using var localProvider = BenchmarkHostFactory.CreateServiceProvider(Package, ActorTypes);
        BenchmarkHostFactory.ResolveRuntime(localProvider, Package);
        return BenchmarkHostFactory.InvokeAsync(localProvider, Package, "startup-probe").GetAwaiter().GetResult();
    }
}
