using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public static IServiceCollection AddDaprPubSubClient(this IServiceCollection services, Action<IServiceProvider, DaprPublishSubscribeClientBuilder>? configure = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        //Register the IHttpClientFactory implementation
        services.AddHttpClient();

        var registration = new Func<IServiceProvider, DaprPublishSubscribeClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            var builder = new DaprPublishSubscribeClientBuilder();
            builder.UseHttpClientFactory(httpClientFactory);

            configure?.Invoke(serviceProvider, builder);

            return builder.Build();
        });

        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                services.TryAddScoped(registration);
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(registration);
                break;
            default:
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(registration);
                break;
        }

        return services;
    }
}
