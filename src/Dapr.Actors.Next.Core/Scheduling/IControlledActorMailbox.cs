using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Core.Scheduling;

/// <summary>
/// Exposes deterministic mailbox execution to controlled schedulers.
/// </summary>
public interface IControlledActorMailbox : IActorMailbox
{
    /// <summary>
    /// Gets the number of turns waiting in the mailbox.
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Gets a value indicating whether the mailbox is currently executing a turn.
    /// </summary>
    bool IsExecuting { get; }

    /// <summary>
    /// Peeks at the next turn without dequeuing it.
    /// </summary>
    ActorTurn? Peek();

    /// <summary>
    /// Starts executing the next turn, if one is available and the mailbox is idle.
    /// </summary>
    Task<bool> ExecuteNextAsync(CancellationToken cancellationToken = default);
}
