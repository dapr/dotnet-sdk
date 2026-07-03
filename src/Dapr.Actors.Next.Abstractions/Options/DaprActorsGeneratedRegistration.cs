using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Stores generated registration callbacks used by <see cref="DaprActorsServiceCollectionExtensions"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DaprActorsGeneratedRegistration
{
    private static readonly object SyncRoot = new();
    private static readonly List<Action<IServiceCollection, DaprActorsOptions>> Registrations = [];

    /// <summary>
    /// Registers a generated service registration callback.
    /// </summary>
    public static void Register(Action<IServiceCollection> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        Register((services, _) => registration(services));
    }

    /// <summary>
    /// Registers a generated service registration callback.
    /// </summary>
    public static void Register(Action<IServiceCollection, DaprActorsOptions> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (SyncRoot)
        {
            Registrations.Add(registration);
        }
    }

    /// <summary>
    /// Applies every generated service registration callback.
    /// </summary>
    public static void Apply(IServiceCollection services)
    {
        Apply(services, new DaprActorsOptions());
    }

    /// <summary>
    /// Applies every generated service registration callback.
    /// </summary>
    public static void Apply(IServiceCollection services, DaprActorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        Action<IServiceCollection, DaprActorsOptions>[] registrations;

        lock (SyncRoot)
        {
            registrations = Registrations.ToArray();
        }

        foreach (var registration in registrations)
        {
            registration(services, options);
        }
    }
}
