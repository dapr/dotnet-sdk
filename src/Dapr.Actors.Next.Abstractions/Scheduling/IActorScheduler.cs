namespace Dapr.Actors.Next.Abstractions.Scheduling;

/// <summary>
/// Schedules actor turns from a runtime-owned mailbox.
/// </summary>
public interface IActorScheduler
{
    /// <summary>
    /// Schedules execution for an actor mailbox.
    /// </summary>
    ValueTask ScheduleAsync(IActorMailbox mailbox, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the runtime may run an idle actor's turn inline on the calling thread
    /// instead of enqueuing it and posting to this scheduler.
    /// </summary>
    /// <remarks>
    /// Inline execution removes the thread-pool hop for uncontended calls while preserving one-turn-at-a-time
    /// semantics via the mailbox execution slot. Schedulers that must observe and order every turn (for
    /// example deterministic test schedulers) leave this <see langword="false"/> so all turns flow through
    /// <see cref="ScheduleAsync"/>.
    /// </remarks>
    bool AllowsInlineExecution => false;
}
