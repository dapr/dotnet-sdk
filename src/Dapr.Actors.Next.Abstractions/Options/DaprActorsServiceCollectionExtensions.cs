using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Registers Dapr Actors Next options. Actor lifetime is runtime activation lifetime and is not configurable as a DI lifetime.
/// </summary>
public static class DaprActorsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Actors Next services.
    /// </summary>
    public static IServiceCollection AddDaprActors(this IServiceCollection services, Action<DaprActorsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        configure ??= _ => { };

        var configuredOptions = new DaprActorsOptions();
        configure(configuredOptions);

        services.AddOptions<DaprActorsOptions>().Configure(options => options.CopyFrom(configuredOptions));
        services.AddSingleton<IValidateOptions<DaprActorsOptions>, DaprActorsOptionsValidator>();
        DaprActorsGeneratedRegistration.Apply(services, configuredOptions);
        return services;
    }
}
