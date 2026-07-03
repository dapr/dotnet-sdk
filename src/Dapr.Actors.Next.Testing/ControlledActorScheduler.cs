using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Scheduling;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Base class for deterministic actor schedulers.
/// </summary>
public abstract class ControlledActorScheduler(int seed, string name) : IActorScheduler
{
    private readonly object syncRoot = new();
    private readonly List<IControlledActorMailbox> mailboxes = [];
    private readonly List<Task> inFlight = [];
    private readonly List<InterleavingTranscriptEntry> transcript = [];
    private TaskCompletionSource<object?> changed = NewSignal();
    private int step;

    /// <summary>
    /// Gets the seed that drives this scheduler.
    /// </summary>
    public int Seed { get; } = seed;

    /// <summary>
    /// Gets the scheduler strategy name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the transcript of turns selected by this scheduler.
    /// </summary>
    public IReadOnlyList<InterleavingTranscriptEntry> Transcript
    {
        get
        {
            lock (syncRoot)
            {
                return transcript.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(IActorMailbox mailbox, CancellationToken cancellationToken = default)
    {
        if (mailbox is not IControlledActorMailbox controlled)
        {
            return ValueTask.CompletedTask;
        }

        lock (syncRoot)
        {
            if (!mailboxes.Contains(controlled))
            {
                mailboxes.Add(controlled);
            }

            SignalChanged();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Runs scheduled actor turns until no executable mailbox or in-flight turn remains.
    /// </summary>
    public async Task RunToIdleAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await StepAsync(cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            Task waiting;
            Task signal;
            lock (syncRoot)
            {
                RemoveCompletedInFlight();
                if (GetAvailableMailboxes().Count > 0)
                {
                    continue;
                }

                if (inFlight.Count == 0)
                {
                    return;
                }

                waiting = Task.WhenAny(inFlight);
                signal = changed.Task;
            }

            await Task.WhenAny(waiting, signal).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts one scheduled actor turn if an executable turn exists.
    /// </summary>
    public Task<bool> StepAsync(CancellationToken cancellationToken = default)
    {
        IControlledActorMailbox mailbox;
        ActorTurn turn;
        lock (syncRoot)
        {
            RemoveCompletedInFlight();
            var available = GetAvailableMailboxes();
            if (available.Count == 0)
            {
                return Task.FromResult(false);
            }

            mailbox = SelectMailbox(available);
            turn = mailbox.Peek() ?? throw new InvalidOperationException("Selected mailbox had no pending turn.");
            transcript.Add(new InterleavingTranscriptEntry(
                ++step,
                Name,
                Seed,
                turn.ActorType,
                turn.ActorId.Value,
                turn.OperationName,
                turn.Kind));
        }

        var task = ExecuteTrackedAsync(mailbox, cancellationToken);
        lock (syncRoot)
        {
            inFlight.Add(task);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Creates another scheduler with the same deterministic seed and strategy.
    /// </summary>
    public abstract ControlledActorScheduler ReplayFromSeed();

    /// <summary>
    /// Selects the next mailbox from the available executable mailboxes.
    /// </summary>
    protected abstract IControlledActorMailbox SelectMailbox(IReadOnlyList<IControlledActorMailbox> available);

    private async Task ExecuteTrackedAsync(IControlledActorMailbox mailbox, CancellationToken cancellationToken)
    {
        try
        {
            await mailbox.ExecuteNextAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (syncRoot)
            {
                SignalChanged();
            }
        }
    }

    private List<IControlledActorMailbox> GetAvailableMailboxes() =>
        mailboxes.Where(mailbox => !mailbox.IsExecuting && mailbox.PendingCount > 0).ToList();

    private void RemoveCompletedInFlight()
    {
        for (var index = inFlight.Count - 1; index >= 0; index--)
        {
            if (inFlight[index].IsCompleted)
            {
                inFlight.RemoveAt(index);
            }
        }
    }

    private void SignalChanged()
    {
        var previous = changed;
        changed = NewSignal();
        previous.TrySetResult(null);
    }

    private static TaskCompletionSource<object?> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
