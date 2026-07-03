using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Creates generated strongly typed actor proxies.
/// </summary>
/// <remarks>
/// This is the preferred API for application code. Resolve this service from dependency injection and use it
/// wherever a service, endpoint, or actor needs to call another actor.
/// </remarks>
public interface IActorProxyFactory
{
    /// <summary>
    /// Creates a proxy for an actor interface.
    /// </summary>
    TActor Create<TActor>(ActorId actorId, string actorType)
        where TActor : IActor;
}
