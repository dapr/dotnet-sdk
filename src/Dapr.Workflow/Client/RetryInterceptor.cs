using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Workflow.Client;

/// <summary>
/// Executes a workflow operation with a retry policy using durable timers.
/// </summary>
/// <typeparam name="T">The operation result type.</typeparam>
/// <param name="context">The workflow context used for scheduling timers.</param>
/// <param name="retryPolicy">The retry policy to apply.</param>
/// <param name="retryCall">The operation to invoke and retry.</param>
/// <param name="retryTimerFactory">
/// Creates the retry delay timer with the appropriate origin metadata.
/// Receives the delay <see cref="TimeSpan"/> and returns a <see cref="Task"/> that completes when the timer fires.
/// When <c>null</c>, falls back to a plain <see cref="WorkflowContext.CreateTimer(TimeSpan, CancellationToken)"/> call.
/// </param>
public sealed class RetryInterceptor<T>(
    IWorkflowContext context,
    WorkflowRetryPolicy retryPolicy,
    Func<Task<T>> retryCall,
    Func<TimeSpan, Task>? retryTimerFactory = null)
{
    /// <summary>
    /// Executes the operation and applies the retry policy when failures occur.
    /// </summary>
    /// <returns>The operation result.</returns>
    public async Task<T?> Invoke()
    {
        Exception? lastException = null;
        var firstAttempt = context.CurrentUtcDateTime;

        for (var retryCount = 0; retryCount < retryPolicy.MaxNumberOfAttempts; retryCount++)
        {
            if (retryCount > 0 && RemainingTime(firstAttempt) <= TimeSpan.Zero)
                break;

            try
            {
                return await retryCall();
            }
            catch (WorkflowTaskFailedException ex) when (!IsFatal(ex))
            {
                lastException = ex;
            }

            var isLastRetry = retryCount + 1 == retryPolicy.MaxNumberOfAttempts;
            if (isLastRetry)
            {
                break;
            }

            var nextDelay = ComputeNextDelay(retryCount, firstAttempt);
            if (nextDelay == TimeSpan.Zero)
                break;

            if (retryTimerFactory != null)
            {
                await retryTimerFactory(nextDelay);
            }
            else if (context is WorkflowContext workflowContext)
            {
                await workflowContext.CreateTimer(nextDelay);
            }
            else
            {
                throw new InvalidOperationException(
                    "RetryInterceptor requires a WorkflowContext to schedule durable retry timers.");
            }
        }

        if (lastException != null)
        {
            ExceptionDispatchInfo.Capture(lastException).Throw();
            throw lastException; // no-op
        }

        return default;
    }

    /// <summary>
    /// Returns true or false whether an exception is considered fatal.
    /// </summary>
    private static bool IsFatal(Exception ex) => (ex is OutOfMemoryException or StackOverflowException);

    private TimeSpan RemainingTime(DateTime firstAttempt)
    {
        var available = DateTime.MaxValue - context.CurrentUtcDateTime;
        if (retryPolicy.RetryTimeout != Timeout.InfiniteTimeSpan && retryPolicy.RetryTimeout != TimeSpan.MaxValue)
            available = TimeSpan.FromTicks(Math.Min(available.Ticks,
                retryPolicy.RetryTimeout.Ticks - (context.CurrentUtcDateTime - firstAttempt).Ticks));
        return available;
    }

    private TimeSpan ComputeNextDelay(int attempt, DateTime firstAttempt)
    {
        var remaining = RemainingTime(firstAttempt);
        if (remaining <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var cap = retryPolicy.MaxRetryInterval is { } maximum && maximum < remaining ? maximum : remaining;
        var ticks = retryPolicy.FirstRetryInterval.Ticks * Math.Pow(retryPolicy.BackoffCoefficient, attempt);
        // Clamp before converting: exponential backoff can exceed both Int64 and Double ranges.
        return ticks >= cap.Ticks ? cap : TimeSpan.FromTicks((long)ticks);
    }
}
