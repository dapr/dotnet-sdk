namespace Dapr.Actors.Next.Abstractions.Scheduling;

/// <summary>
/// Represents the runtime-owned mailbox for one actor activation.
/// </summary>
public interface IActorMailbox
{
    /// <summary>
    /// Gets the actor type name.
    /// </summary>
    string ActorType { get; }

    /// <summary>
    /// Gets the actor id.
    /// </summary>
    ActorId ActorId { get; }

    /// <summary>
    /// Enqueues a turn.
    /// </summary>
    ValueTask EnqueueAsync(ActorTurn turn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to dequeue the next turn.
    /// </summary>
    ValueTask<ActorTurn?> TryDequeueAsync(CancellationToken cancellationToken = default);
}
