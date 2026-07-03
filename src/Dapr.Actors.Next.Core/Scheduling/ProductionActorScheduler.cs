using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Core.Scheduling;

/// <summary>
/// Production scheduler that drains each actor mailbox one turn at a time.
/// </summary>
public sealed class ProductionActorScheduler : IActorScheduler
{
    /// <inheritdoc />
    public bool AllowsInlineExecution => true;

    /// <inheritdoc />
    public ValueTask ScheduleAsync(IActorMailbox mailbox, CancellationToken cancellationToken = default)
    {
        if (mailbox is RuntimeActorMailbox runtimeMailbox && runtimeMailbox.TryScheduleDrain(cancellationToken))
        {
            _ = Task.Run(runtimeMailbox.DrainScheduledAsync, CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }
}
