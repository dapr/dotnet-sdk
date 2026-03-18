using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Workflow;

namespace Dapr.Workflow.Client;

/// <summary>
/// Executes a workflow operation with a retry policy using durable timers.
/// </summary>
/// <typeparam name="T">The operation result type.</typeparam>
/// <param name="context">The workflow context used for scheduling timers.</param>
/// <param name="retryPolicy">The retry policy to apply.</param>
/// <param name="retryCall">The operation to invoke and retry.</param>
public sealed class RetryInterceptor<T>(IWorkflowContext context, WorkflowRetryPolicy retryPolicy, Func<Task<T>> retryCall)
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

            var nextDelay = ComputeNextDelay(retryCount, firstAttempt, lastException);
            if (nextDelay == TimeSpan.Zero)
                break;

            if (context is WorkflowContext workflowContext)
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

    private TimeSpan ComputeNextDelay(int attempt, DateTime firstAttempt, Exception failure)
    {
        var nextDelay = TimeSpan.Zero;
        try
        {
            var retryExpiration = (retryPolicy.RetryTimeout != Timeout.InfiniteTimeSpan &&
                                   retryPolicy.RetryTimeout != TimeSpan.MaxValue)
                ? firstAttempt.Add(retryPolicy.RetryTimeout)
                : DateTime.MaxValue;

            if (context.CurrentUtcDateTime < retryExpiration)
            {
                var nextDelayInMilliseconds = retryPolicy.FirstRetryInterval.TotalMilliseconds *
                                              Math.Pow(retryPolicy.BackoffCoefficient, attempt);
                nextDelay = TimeSpan.FromMilliseconds(nextDelayInMilliseconds);
                if (retryPolicy.MaxRetryInterval is { } maxInterval && nextDelay > maxInterval)
                {
                    nextDelay = maxInterval;
                }
            }
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            
        }

        return nextDelay;
    }
}


