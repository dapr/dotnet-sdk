using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Entry point for generated strongly typed actor proxies.
/// </summary>
public static class ActorProxy
{
    private static IActorProxyFactory? _factory;

    /// <summary>
    /// Configures the process-wide generated proxy factory.
    /// </summary>
    public static void Configure(IActorProxyFactory proxyFactory)
    {
        _factory = proxyFactory ?? throw new ArgumentNullException(nameof(proxyFactory));
    }

    /// <summary>
    /// Clears the configured proxy factory.
    /// </summary>
    public static void Reset()
    {
        _factory = null;
    }

    /// <summary>
    /// Creates a strongly typed actor proxy.
    /// </summary>
    public static TActor Create<TActor>(ActorId actorId, string actorType)
        where TActor : IActor
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("ActorProxy is not configured. The source generator must provide an IActorProxyFactory.");
        }

        return _factory.Create<TActor>(actorId, actorType);
    }
}
