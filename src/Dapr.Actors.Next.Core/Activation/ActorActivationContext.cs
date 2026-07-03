using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Core.Activation;

/// <summary>
/// Exposes activation-scoped runtime services to hand-written factories and generated factories.
/// </summary>
public sealed class ActorActivationContext
{
    internal ActorActivationContext(ActorId actorId, IActorStateAccessor state)
    {
        ActorId = actorId;
        State = state;
    }

    /// <summary>
    /// Gets the actor id for the activation being constructed.
    /// </summary>
    public ActorId ActorId { get; }

    /// <summary>
    /// Gets the state accessor for the activation being constructed.
    /// </summary>
    public IActorStateAccessor State { get; }
}
