namespace Dapr.Actors.Next.Abstractions.Dispatching;

/// <summary>
/// Creates an actor instance for one runtime activation.
/// </summary>
public delegate TActor ActorFactory<out TActor>(IServiceProvider services, ActorId actorId)
    where TActor : IActor;
