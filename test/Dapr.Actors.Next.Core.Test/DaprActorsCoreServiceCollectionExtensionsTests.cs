using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Core.Transport;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Test;

public sealed class DaprActorsCoreServiceCollectionExtensionsTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_registers_generated_dapr_grpc_client_with_workflow_style_channel_options()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_GRPC_ENDPOINT"] = "http://127.0.0.1:51001",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprActorsCore(_ => { });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<P.Dapr.DaprClient>();
        var options = ApplyGrpcOptions(provider);

        Assert.NotNull(client);
        Assert.IsType<DaprActorInvocationClient>(provider.GetRequiredService<IActorInvocationClient>());
        Assert.IsType<DaprSidecarActorStateStore>(provider.GetRequiredService<IActorStateStore>());
        Assert.IsType<DaprSidecarActorTimerScheduler>(provider.GetRequiredService<IActorTimerScheduler>());
        Assert.IsType<DaprActorEventsTransport>(provider.GetRequiredService<ISubscribeActorEventsTransport>());
        Assert.IsType<SocketsHttpHandler>(options.HttpHandler);
        var handler = (SocketsHttpHandler)options.HttpHandler!;
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.ConnectTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.PooledConnectionIdleTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
        Assert.Equal(TimeSpan.FromSeconds(60), handler.KeepAlivePingDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), handler.KeepAlivePingTimeout);
        Assert.Equal(HttpKeepAlivePingPolicy.Always, handler.KeepAlivePingPolicy);
        Assert.True(handler.EnableMultipleHttp2Connections);
        Assert.Null(options.MaxReceiveMessageSize);
        Assert.Null(options.MaxSendMessageSize);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActorsCore_preserves_existing_generated_dapr_grpc_client()
    {
        var services = new ServiceCollection();
        var customClient = new TestDaprClient();
        services.AddSingleton<P.Dapr.DaprClient>(customClient);

        services.AddDaprActorsCore(_ => { });

        using var provider = services.BuildServiceProvider();

        Assert.Same(customClient, provider.GetRequiredService<P.Dapr.DaprClient>());
    }

    private static GrpcChannelOptions ApplyGrpcOptions(ServiceProvider provider)
    {
        var channelOptions = new GrpcChannelOptions();
        var monitor = provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>();
        var clientType = typeof(P.Dapr.DaprClient);
        var optionCandidates = new[]
        {
            monitor.Get(clientType.FullName!),
            monitor.Get(clientType.Name),
        };

        foreach (var options in optionCandidates.Distinct())
        {
            foreach (var action in options.ChannelOptionsActions)
            {
                action(channelOptions);
            }
        }

        return channelOptions;
    }

    private sealed class TestDaprClient : P.Dapr.DaprClient
    {
    }
}
