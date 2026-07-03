using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core.Runtime;

namespace Dapr.Actors.Next.Core.Scheduling;

/// <summary>
/// Runtime mailbox for one actor activation key.
/// </summary>
public sealed class RuntimeActorMailbox(string actorType, ActorId actorId, ActorRuntime runtime) : IControlledActorMailbox
{
    private readonly Queue<ActorTurnWork> work = [];
    private readonly object syncRoot = new();
    private bool executing;
    private bool drainScheduled;

    /// <inheritdoc />
    public string ActorType { get; } = actorType;

    /// <inheritdoc />
    public ActorId ActorId { get; } = actorId;

    /// <inheritdoc />
    public int PendingCount
    {
        get
        {
            lock (syncRoot)
            {
                return work.Count;
            }
        }
    }

    /// <inheritdoc />
    public bool IsExecuting
    {
        get
        {
            lock (syncRoot)
            {
                return executing;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask EnqueueAsync(ActorTurn turn, CancellationToken cancellationToken = default)
    {
        var request = new ActorRuntimeRequest(turn.ActorType, turn.ActorId, turn.OperationName, turn.Kind, ReadOnlyMemory<byte>.Empty, turn.Headers, turn.RequestContext);
        return EnqueueWorkAsync(new ActorTurnWork(request, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<ActorTurn?> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            return work.TryDequeue(out var item)
                ? ValueTask.FromResult<ActorTurn?>(ToActorTurn(item.Request))
                : ValueTask.FromResult<ActorTurn?>(null);
        }
    }

    /// <inheritdoc />
    public ActorTurn? Peek()
    {
        lock (syncRoot)
        {
            return work.TryPeek(out var item) ? ToActorTurn(item.Request) : null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteNextAsync(CancellationToken cancellationToken = default)
    {
        var item = TryDequeueForExecution();
        if (item is null)
        {
            return false;
        }

        try
        {
            item.Complete(await runtime.ExecuteTurnAsync(item.Request, CreateExecutionCancellationToken(item, cancellationToken)).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            item.Fail(ex);
        }
        finally
        {
            FinishExecution();
        }

        return true;
    }

    /// <summary>
    /// Attempts to claim the (single) execution slot so the caller can run its turn directly, avoiding the
    /// enqueue + thread-pool hop. Succeeds only when the mailbox is idle and empty, preserving the
    /// one-turn-at-a-time guarantee. When it fails the caller must fall back to the enqueue + schedule path.
    /// </summary>
    internal bool TryClaimInlineTurn()
    {
        lock (syncRoot)
        {
            if (executing || work.Count > 0)
            {
                return false;
            }

            executing = true;
            return true;
        }
    }

    /// <summary>
    /// Releases the execution slot claimed by <see cref="TryClaimInlineTurn"/>. Returns <see langword="true"/>
    /// when turns were enqueued while the inline turn ran and therefore need a drain to be scheduled.
    /// </summary>
    internal bool ReleaseInlineTurn()
    {
        lock (syncRoot)
        {
            executing = false;
            return work.Count > 0;
        }
    }

    internal ValueTask EnqueueWorkAsync(ActorTurnWork item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            work.Enqueue(item);
        }

        return ValueTask.CompletedTask;
    }

    internal bool TryScheduleDrain(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            if (drainScheduled || work.Count == 0)
            {
                return false;
            }

            drainScheduled = true;
            return true;
        }
    }

    internal async Task DrainScheduledAsync()
    {
        while (true)
        {
            var item = TryDequeueForExecution();
            if (item is null)
            {
                // Either the queue drained or an inline turn currently holds the execution slot. Only give up
                // when there is genuinely nothing left for this pump to do; if work is queued and the slot is
                // free (an inline turn released between our attempts, or an item arrived after we saw empty),
                // loop again to claim it. The inline release path reschedules a drain when it observes queued
                // work, so no work is ever stranded.
                lock (syncRoot)
                {
                    if (work.Count == 0 || executing)
                    {
                        drainScheduled = false;
                        return;
                    }
                }

                continue;
            }

            try
            {
                item.Complete(await runtime.ExecuteTurnAsync(item.Request, item.CancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                item.Fail(ex);
            }
            finally
            {
                FinishExecution();
            }
        }
    }

    internal async ValueTask DrainAsync(CancellationToken cancellationToken)
    {
        while (PendingCount > 0)
        {
            if (!await ExecuteNextAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private ActorTurnWork? TryDequeueForExecution()
    {
        lock (syncRoot)
        {
            if (executing || !work.TryDequeue(out var item))
            {
                return null;
            }

            executing = true;
            return item;
        }
    }

    private void FinishExecution()
    {
        lock (syncRoot)
        {
            executing = false;
        }
    }

    private static CancellationToken CreateExecutionCancellationToken(ActorTurnWork item, CancellationToken schedulerCancellationToken)
    {
        return schedulerCancellationToken.CanBeCanceled
            ? schedulerCancellationToken
            : item.CancellationToken;
    }

    private static ActorTurn ToActorTurn(ActorRuntimeRequest request) =>
        new(request.ActorType, request.ActorId, request.OperationName, request.Kind, request.RequestContext, request.Headers);
}
