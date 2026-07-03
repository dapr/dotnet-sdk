using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Creates generated strongly typed actor proxies.
/// </summary>
public interface IActorProxyFactory
{
    /// <summary>
    /// Creates a proxy for an actor interface.
    /// </summary>
    TActor Create<TActor>(ActorId actorId, string actorType)
        where TActor : IActor;
}
