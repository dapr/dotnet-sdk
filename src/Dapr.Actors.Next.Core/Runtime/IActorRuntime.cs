using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Core.Client;

namespace Dapr.Actors.Next.Core.Runtime;

/// <summary>
/// Dispatches inbound runtime callbacks to actor activations.
/// </summary>
public interface IActorRuntime : IActorInvocationClient
{
    /// <summary>
    /// Dispatches an inbound callback and returns the serialized result bytes.
    /// </summary>
    Task<byte[]?> DispatchAsync(ActorRuntimeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates an actor activation if present.
    /// </summary>
    Task DeactivateAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default);
}
