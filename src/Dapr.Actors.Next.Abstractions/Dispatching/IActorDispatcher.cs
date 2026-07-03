namespace Dapr.Actors.Next.Abstractions.Dispatching;

/// <summary>
/// Dispatches actor turns to concrete actor methods.
/// </summary>
public interface IActorDispatcher
{
    /// <summary>
    /// Dispatches a request to an actor instance.
    /// </summary>
    ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default);
}
