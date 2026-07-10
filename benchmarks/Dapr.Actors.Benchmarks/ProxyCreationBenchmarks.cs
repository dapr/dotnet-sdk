using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class ProxyCreationBenchmarks
{
    private ServiceProvider provider = null!;

    [Params(ActorPackage.DaprActors, ActorPackage.DaprActorsNext)]
    public ActorPackage Package { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        provider = BenchmarkHostFactory.CreateServiceProvider(Package, actorTypes: 1);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        provider.Dispose();
    }

    [Benchmark]
    public object CreateProxy()
    {
        return BenchmarkHostFactory.CreateProxy(provider, Package);
    }
}
