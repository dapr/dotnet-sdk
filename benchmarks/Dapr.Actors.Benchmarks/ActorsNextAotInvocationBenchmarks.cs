using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Benchmarks;

[Config(typeof(ActorsNextAotConfig))]
[MemoryDiagnoser]
public class ActorsNextAotInvocationBenchmarks
{
    private const int ParallelCalls = 32;
    private ServiceProvider provider = null!;
    private int nextActorId;

    [GlobalSetup]
    public async Task Setup()
    {
        provider = BenchmarkHostFactory.CreateActorsNextServiceProvider(actorTypes: 1, useAotSerializer: true);
        await BenchmarkHostFactory.InvokeAsync(provider, ActorPackage.DaprActorsNext, "warm").ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        provider.Dispose();
    }

    [Benchmark]
    public Task<int> WarmActorCall()
    {
        return BenchmarkHostFactory.InvokeAsync(provider, ActorPackage.DaprActorsNext, "warm");
    }

    [Benchmark]
    public Task<int> ColdActorActivationCall()
    {
        var id = Interlocked.Increment(ref nextActorId).ToString();
        return BenchmarkHostFactory.InvokeAsync(provider, ActorPackage.DaprActorsNext, id);
    }

    [Benchmark]
    public async Task<int> ParallelFanOut()
    {
        var start = Interlocked.Add(ref nextActorId, ParallelCalls);
        var tasks = new Task<int>[ParallelCalls];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = BenchmarkHostFactory.InvokeAsync(provider, ActorPackage.DaprActorsNext, "aot-parallel-" + (start - i));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Sum();
    }
}
