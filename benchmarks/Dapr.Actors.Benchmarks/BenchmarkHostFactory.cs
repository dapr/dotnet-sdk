using Dapr.Actors.Client;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Runtime;
using Dapr.Common.Serialization;
using Microsoft.Extensions.DependencyInjection;
using LegacyActorId = Dapr.Actors.ActorId;
using LegacyActorProxyFactory = Dapr.Actors.Client.ActorProxyFactory;
using LegacyIActorProxyFactory = Dapr.Actors.Client.IActorProxyFactory;
using LegacyActorRuntime = Dapr.Actors.Runtime.ActorRuntime;
using NextActorId = Dapr.Actors.Next.Abstractions.ActorId;
using NextIActorProxyFactory = Dapr.Actors.Next.Core.Client.IActorProxyFactory;

namespace Dapr.Actors.Benchmarks;

internal static class BenchmarkHostFactory
{
    public const string ActorType = "BenchmarkActor01";
    public const string MethodName = nameof(INextBenchmarkActor.AddAsync);

    private static readonly byte[] OneJsonPayload = "1"u8.ToArray();
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>();

    public static ServiceProvider CreateServiceProvider(ActorPackage package, int actorTypes)
    {
        return package switch
        {
            ActorPackage.DaprActors => CreateLegacyServiceProvider(actorTypes),
            ActorPackage.DaprActorsNext => CreateActorsNextServiceProvider(actorTypes),
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null),
        };
    }

    public static object CreateProxy(ServiceProvider provider, ActorPackage package)
    {
        return package switch
        {
            ActorPackage.DaprActors => provider.GetRequiredService<LegacyIActorProxyFactory>()
                .CreateActorProxy<ILegacyBenchmarkActor>(new LegacyActorId("proxy"), ActorType),
            ActorPackage.DaprActorsNext => provider.GetRequiredService<NextIActorProxyFactory>()
                .Create<INextBenchmarkActor01>(NextActorId.Create("proxy"), ActorType),
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null),
        };
    }

    public static object ResolveRuntime(ServiceProvider provider, ActorPackage package)
    {
        return package switch
        {
            ActorPackage.DaprActors => provider.GetRequiredService<LegacyActorRuntime>(),
            ActorPackage.DaprActorsNext => provider.GetRequiredService<IActorRuntime>(),
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null),
        };
    }

    public static Task<int> InvokeAsync(ServiceProvider provider, ActorPackage package, string actorId)
    {
        return package switch
        {
            ActorPackage.DaprActors => InvokeLegacyAsync(provider.GetRequiredService<LegacyActorRuntime>(), actorId),
            ActorPackage.DaprActorsNext => InvokeNextAsync(provider.GetRequiredService<IActorRuntime>(), actorId),
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null),
        };
    }

    private static ServiceProvider CreateLegacyServiceProvider(int actorTypes)
    {
        var services = new ServiceCollection();
        services.AddSingleton<LegacyIActorProxyFactory>(new LegacyActorProxyFactory());
        services.AddActors(options => RegisterLegacyActors(options, actorTypes));
        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateActorsNextSdkOnlyServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDaprActors(options =>
        {
            options.EnableAutoActorRegistration = false;
            options.DisableStateMigration = true;
            options.Actors.RegisterActor<NextBenchmarkActor01>(ActorType);
        });

        services.AddSingleton<IActorInvocationClient, BenchmarkNoopActorInvocationClient>();
        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateActorsNextServiceProvider(int actorTypes, bool useAotSerializer = false)
    {
        var services = new ServiceCollection();
        if (useAotSerializer)
        {
            services.AddSingleton<IDaprSerializer, BenchmarkAotDaprSerializer>();
            services.AddSingleton<IActorWireSerializer, BenchmarkAotWireSerializer>();
        }

        services.AddDaprActors(options =>
        {
            options.EnableAutoActorRegistration = false;
            options.DisableStateMigration = true;
            RegisterNextActors(options, actorTypes);
        });

        return services.BuildServiceProvider();
    }

    private static async Task<int> InvokeLegacyAsync(LegacyActorRuntime runtime, string actorId)
    {
        await using var input = new MemoryStream(OneJsonPayload, writable: false);
        await using var output = new MemoryStream();
        await runtime.DispatchWithoutRemotingAsync(ActorType, actorId, MethodName, input, output).ConfigureAwait(false);
        return (int)output.Length;
    }

    private static async Task<int> InvokeNextAsync(IActorRuntime runtime, string actorId)
    {
        var response = await runtime.InvokeAsync(ActorType, actorId, MethodName, OneJsonPayload, EmptyHeaders).ConfigureAwait(false);
        return response?.Length ?? 0;
    }

    private static void RegisterLegacyActors(ActorRuntimeOptions options, int actorTypes)
    {
        if (actorTypes >= 1) options.Actors.RegisterActor<LegacyBenchmarkActor01>(ActorType);
        if (actorTypes >= 2) options.Actors.RegisterActor<LegacyBenchmarkActor02>("BenchmarkActor02");
        if (actorTypes >= 3) options.Actors.RegisterActor<LegacyBenchmarkActor03>("BenchmarkActor03");
        if (actorTypes >= 4) options.Actors.RegisterActor<LegacyBenchmarkActor04>("BenchmarkActor04");
        if (actorTypes >= 5) options.Actors.RegisterActor<LegacyBenchmarkActor05>("BenchmarkActor05");
        if (actorTypes >= 6) options.Actors.RegisterActor<LegacyBenchmarkActor06>("BenchmarkActor06");
        if (actorTypes >= 7) options.Actors.RegisterActor<LegacyBenchmarkActor07>("BenchmarkActor07");
        if (actorTypes >= 8) options.Actors.RegisterActor<LegacyBenchmarkActor08>("BenchmarkActor08");
    }

    private static void RegisterNextActors(DaprActorsOptions options, int actorTypes)
    {
        if (actorTypes >= 1) options.Actors.RegisterActor<NextBenchmarkActor01>(ActorType);
        if (actorTypes >= 2) options.Actors.RegisterActor<NextBenchmarkActor02>("BenchmarkActor02");
        if (actorTypes >= 3) options.Actors.RegisterActor<NextBenchmarkActor03>("BenchmarkActor03");
        if (actorTypes >= 4) options.Actors.RegisterActor<NextBenchmarkActor04>("BenchmarkActor04");
        if (actorTypes >= 5) options.Actors.RegisterActor<NextBenchmarkActor05>("BenchmarkActor05");
        if (actorTypes >= 6) options.Actors.RegisterActor<NextBenchmarkActor06>("BenchmarkActor06");
        if (actorTypes >= 7) options.Actors.RegisterActor<NextBenchmarkActor07>("BenchmarkActor07");
        if (actorTypes >= 8) options.Actors.RegisterActor<NextBenchmarkActor08>("BenchmarkActor08");
    }
}
