using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using LegacyActorId = Dapr.Actors.ActorId;
using LegacyActorProxy = Dapr.Actors.Client.ActorProxy;
using LegacyActorProxyFactory = Dapr.Actors.Client.ActorProxyFactory;
using LegacyActorProxyOptions = Dapr.Actors.Client.ActorProxyOptions;
using NextActorId = Dapr.Actors.Next.Abstractions.ActorId;
using NextIActorProxyFactory = Dapr.Actors.Next.Core.Client.IActorProxyFactory;

namespace Dapr.Actors.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class SdkOnlyInvocationBenchmarks
{
    private ServiceProvider? provider;
    private LegacyActorProxy legacyProxy = null!;
    private INextBenchmarkActor01 nextProxy = null!;

    [Params(ActorPackage.DaprActors, ActorPackage.DaprActorsNext)]
    public ActorPackage Package { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        switch (Package)
        {
            case ActorPackage.DaprActors:
                var options = new LegacyActorProxyOptions
                {
                    DaprApiToken = null,
                    HttpEndpoint = "http://127.0.0.1",
                };
                var factory = new LegacyActorProxyFactory(options, new BenchmarkNoopActorHttpMessageHandler());
                legacyProxy = factory.Create(new LegacyActorId("sdk-only"), BenchmarkHostFactory.ActorType);
                break;

            case ActorPackage.DaprActorsNext:
                provider = BenchmarkHostFactory.CreateActorsNextSdkOnlyServiceProvider();
                nextProxy = provider.GetRequiredService<NextIActorProxyFactory>()
                    .Create<INextBenchmarkActor01>(NextActorId.Create("sdk-only"), BenchmarkHostFactory.ActorType);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Package), Package, null);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        provider?.Dispose();
    }

    [Benchmark]
    public Task<int> InvokeAddAsyncNoRuntime()
    {
        return Package switch
        {
            ActorPackage.DaprActors => legacyProxy.InvokeMethodAsync<int, int>(BenchmarkHostFactory.MethodName, 1),
            ActorPackage.DaprActorsNext => nextProxy.AddAsync(1),
            _ => throw new ArgumentOutOfRangeException(nameof(Package), Package, null),
        };
    }
}
