using Dapr.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging.PublishSubscribe.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Publish/Subscribe with dependency injection.
/// </summary>
public static class PublishSubscribeServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Publish/Subscribe support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprPublishSubscribeClient"/> using injected services.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    /// <returns></returns>
    public static IDaprPubSubBuilder AddDaprPubSubClient(
        this IServiceCollection services,
        Action<IServiceProvider, DaprPublishSubscribeClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services.AddDaprClient<DaprPublishSubscribeClient, DaprPublishSubscribeGrpcClient, DaprPubSubBuilder, DaprPublishSubscribeClientBuilder>(
            configure, lifetime);
}
