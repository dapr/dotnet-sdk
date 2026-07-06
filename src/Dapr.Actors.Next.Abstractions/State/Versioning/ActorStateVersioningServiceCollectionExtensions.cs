using Dapr.Actors.Next.Abstractions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Configures actor state versioning conventions.
/// </summary>
public static class ActorStateVersioningServiceCollectionExtensions
{
    /// <summary>
    /// Configures the application-wide compile-time actor state versioning strategy.
    /// </summary>
    public static IServiceCollection UseActorStateVersioning<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IActorStateVersionStrategy
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IActorStateVersionStrategy, TStrategy>();
        services.AddSingleton<IActorStateVersionSelector, MaxActorStateVersionSelector>();
        services.Configure<DaprActorsOptions>(options => options.ActorStateVersionStrategyType = ActorStateVersionStrategyType<TStrategy>.Instance);
        return services;
    }
}
