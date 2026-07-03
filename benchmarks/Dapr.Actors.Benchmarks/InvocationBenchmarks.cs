using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class InvocationBenchmarks
{
    private const int ParallelCalls = 32;
    private ServiceProvider provider = null!;
    private int nextActorId;

    [Params(ActorPackage.DaprActors, ActorPackage.DaprActorsNext)]
    public ActorPackage Package { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        provider = BenchmarkHostFactory.CreateServiceProvider(Package, actorTypes: 1);
        await BenchmarkHostFactory.InvokeAsync(provider, Package, "warm").ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        provider.Dispose();
    }

    [Benchmark]
    public Task<int> WarmActorCall()
    {
        return BenchmarkHostFactory.InvokeAsync(provider, Package, "warm");
    }

    [Benchmark]
    public Task<int> ColdActorActivationCall()
    {
        var id = Interlocked.Increment(ref nextActorId).ToString();
        return BenchmarkHostFactory.InvokeAsync(provider, Package, id);
    }

    [Benchmark]
    public async Task<int> ParallelFanOut()
    {
        var start = Interlocked.Add(ref nextActorId, ParallelCalls);
        var tasks = new Task<int>[ParallelCalls];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = BenchmarkHostFactory.InvokeAsync(provider, Package, "parallel-" + (start - i));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Sum();
    }
}
