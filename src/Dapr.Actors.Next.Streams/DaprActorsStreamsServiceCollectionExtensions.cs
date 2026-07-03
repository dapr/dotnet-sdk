using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Registers Dapr Actors Next stream delivery services.
/// </summary>
public static class DaprActorsStreamsServiceCollectionExtensions
{
    /// <summary>
    /// Adds host-owned pub/sub to actor stream delivery services.
    /// </summary>
    public static IServiceCollection AddDaprActorStreams(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ActorStreamSubscriptionRegistry>();
        services.TryAddSingleton<IActorStreamSubscriptionRegistry>(sp => sp.GetRequiredService<ActorStreamSubscriptionRegistry>());
        services.TryAddSingleton<ActorStreamRoutingKeyExtractor>();
        services.TryAddSingleton<ActorStreamForwarder>();
        services.TryAddSingleton<IActorStreamFailureClassifier, DefaultActorStreamFailureClassifier>();
        services.TryAddSingleton<ActorStreamSubscriptionRunner>();
        services.TryAddSingleton<DaprMessagingActorStreamSubscriber>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ActorStreamSubscriptionHostedService>());
        return services;
    }
}
