using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.NativeAot;

namespace Dapr.Actors.Benchmarks;

public sealed class ActorsNextAotConfig : ManualConfig
{
    public ActorsNextAotConfig()
    {
        AddJob(Job.ShortRun
            .WithRuntime(CoreRuntime.Core10_0)
            .WithId(".NET 10 JIT")
            .AsBaseline());

        AddJob(Job.ShortRun
            .WithRuntime(NativeAotRuntime.Net10_0)
            .WithToolchain(NativeAotToolchain.Net10_0)
            .WithId(".NET 10 Native AOT"));
    }
}
