using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Scheduling;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Core.Transport;
using Dapr.Common.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.DependencyInjection;

/// <summary>
/// Registers Dapr Actors Next Core runtime services.
/// </summary>
public static class DaprActorsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds Core actor runtime services and generated actor registrations.
    /// </summary>
    public static IServiceCollection AddDaprActorsCore(this IServiceCollection services, Action<ActorRuntimeRegistrationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ActorRuntimeRegistrationBuilder();
        configure(builder);

        EnsureDaprActorGrpcClientRegistered(services);

        services.AddOptions<DaprActorsOptions>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IDaprSerializer, JsonDaprSerializer>();
        services.TryAddSingleton<IActorWireSerializer, ActorWireSerializer>();
        services.TryAddSingleton<IActorStateStore>(sp =>
            ShouldUseSidecar(sp)
                ? new DaprSidecarActorStateStore(CreateDaprClientAccessor(sp), daprApiToken: GetDaprApiToken(sp))
                : new InMemoryActorStateStore());
        services.TryAddSingleton<IActorStateFaultInjector, NoopActorStateFaultInjector>();
        services.TryAddSingleton<IActorScheduler, ProductionActorScheduler>();
        services.TryAddSingleton<IActorRuntime, ActorRuntime>();
        services.TryAddSingleton<IActorTimerScheduler>(sp =>
            ShouldUseSidecar(sp)
                ? new DaprSidecarActorTimerScheduler(CreateDaprClientAccessor(sp), sp.GetRequiredService<IActorWireSerializer>(), GetDaprApiToken(sp))
                : new CoreActorTimerScheduler(
                    sp.GetRequiredService<IActorRuntime>(),
                    sp.GetRequiredService<IActorWireSerializer>(),
                    sp.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IActorInvocationClient>(sp =>
            ShouldUseSidecar(sp)
                ? new DaprActorInvocationClient(CreateDaprClientAccessor(sp), GetDaprApiToken(sp))
                : sp.GetRequiredService<IActorRuntime>());
        services.TryAddSingleton<IDynamicActorClient, DynamicActorClient>();
        services.TryAddSingleton<ISubscribeActorEventsTransport>(sp =>
            ShouldUseSidecar(sp)
                ? new DaprActorEventsTransport(CreateDaprClientAccessor(sp), GetDaprApiToken(sp), GetDaprGrpcEndpoint(sp))
                : new DisabledSubscribeActorEventsTransport());
        services.AddSingleton(sp => builder.Build(sp));
        services.AddSingleton<SubscribeActorEventsStreamManager>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SubscribeActorEventsStreamManager>());

        return services;
    }

    private static void EnsureDaprActorGrpcClientRegistered(IServiceCollection services)
    {
        if (services.Any(static service => service.ServiceType == typeof(P.Dapr.DaprClient)))
        {
            return;
        }

        services.AddHttpClient();

        services.AddGrpcClient<P.Dapr.DaprClient>((serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetService<IConfiguration>();
                var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);
                var grpcEndpointUri = new Uri(grpcEndpoint);

                if (grpcEndpointUri.Scheme == Uri.UriSchemeHttp)
                {
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                }

                options.Address = grpcEndpointUri;
                options.ChannelOptionsActions.Add(channelOptions =>
                {
                    channelOptions.HttpHandler = new SocketsHttpHandler
                    {
                        ConnectTimeout = Timeout.InfiniteTimeSpan,
                        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                        PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
                        EnableMultipleHttp2Connections = true,
                    };

                    channelOptions.MaxReceiveMessageSize = null;
                    channelOptions.MaxSendMessageSize = null;
                });
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectTimeout = Timeout.InfiniteTimeSpan,
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
                EnableMultipleHttp2Connections = true,
            })
            .ConfigureHttpClient(static httpClient =>
            {
                httpClient.Timeout = Timeout.InfiniteTimeSpan;
            });
    }

    private static string? GetDaprApiToken(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetService<IConfiguration>();
        return DaprDefaults.GetDefaultDaprApiToken(configuration);
    }

    private static string GetDaprGrpcEndpoint(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetService<IConfiguration>();
        return DaprDefaults.GetDefaultGrpcEndpoint(configuration);
    }

    /// <summary>
    /// Determines whether the sidecar (gRPC) transport should back actor state, timers, invocation, and the
    /// event stream. The sidecar is used by default whenever a Dapr gRPC client is registered. The check does
    /// not construct the gRPC client (it uses <see cref="IServiceProviderIsService"/>), so selecting the store
    /// does not eagerly build the transport channel.
    /// </summary>
    private static bool ShouldUseSidecar(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<IOptions<DaprActorsOptions>>()?.Value is { EnableSidecarTransport: false })
        {
            return false;
        }

        // When the container cannot be introspected, default to the sidecar route (the standard registration
        // path always registers a Dapr gRPC client). The client itself is still resolved lazily on first use.
        var isService = serviceProvider.GetService<IServiceProviderIsService>();
        return isService?.IsService(typeof(P.Dapr.DaprClient)) ?? true;
    }

    /// <summary>
    /// Creates a lazy accessor that resolves the Dapr gRPC client on first use rather than while wiring up the
    /// runtime, so resolving the runtime no longer forces construction of the gRPC/HttpClientFactory graph.
    /// </summary>
    private static Lazy<P.Dapr.DaprClient> CreateDaprClientAccessor(IServiceProvider serviceProvider) =>
        new(serviceProvider.GetRequiredService<P.Dapr.DaprClient>);
}
