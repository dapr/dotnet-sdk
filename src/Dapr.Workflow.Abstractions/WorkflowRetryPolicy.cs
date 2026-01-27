// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Workflow;

/// <summary>
/// A declarative retry policy that can be configured for activity or child workflow calls.
/// </summary>
public class WorkflowRetryPolicy
{
    /// <summary>
    /// A declarative retry policy that can be configured for activity or child workflow calls.
    /// </summary>
    /// <param name="maxNumberOfAttempts">The maximum number of task invocation attempts. Must be 1 or greater.</param>
    /// <param name="firstRetryInterval">The amount of time to delay between the first and second attempt.</param>
    /// <param name="backoffCoefficient">
    /// The exponential back-off coefficient used to determine the delay between subsequent retries. Must be 1.0 or greater.
    /// </param>
    /// <param name="maxRetryInterval">
    /// The maximum time to delay between attempts, regardless of<paramref name="backoffCoefficient"/>.
    /// </param>
    /// <param name="retryTimeout">The overall timeout for retries.</param>
    /// <remarks>
    /// The value <see cref="Timeout.InfiniteTimeSpan"/> can be used to specify an unlimited timeout for
    /// <paramref name="maxRetryInterval"/> or <paramref name="retryTimeout"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if any of the following are true:
    /// <list type="bullet">
    ///   <item>The value for <paramref name="maxNumberOfAttempts"/> is less than or equal to zero.</item>
    ///   <item>The value for <paramref name="firstRetryInterval"/> is less than or equal to <see cref="TimeSpan.Zero"/>.</item>
    ///   <item>The value for <paramref name="backoffCoefficient"/> is less than 1.0.</item>
    ///   <item>The value for <paramref name="maxRetryInterval"/> is less than <paramref name="firstRetryInterval"/>.</item>
    ///   <item>The value for <paramref name="retryTimeout"/> is less than <paramref name="firstRetryInterval"/>.</item>
    /// </list>
    /// </exception>
    public WorkflowRetryPolicy(int maxNumberOfAttempts,
        TimeSpan firstRetryInterval,
        double backoffCoefficient = 1.0,
        TimeSpan? maxRetryInterval = null,
        TimeSpan? retryTimeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxNumberOfAttempts, 0, nameof(maxNumberOfAttempts));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(firstRetryInterval, TimeSpan.Zero, nameof(firstRetryInterval));
        ArgumentOutOfRangeException.ThrowIfLessThan(backoffCoefficient, 1.0, nameof(backoffCoefficient));

        var resolvedMaxRetryInterval = maxRetryInterval ?? TimeSpan.FromHours(1);
        ArgumentOutOfRangeException.ThrowIfLessThan(resolvedMaxRetryInterval, firstRetryInterval, nameof(maxRetryInterval));

        var resolvedRetryTimeout = retryTimeout ?? Timeout.InfiniteTimeSpan;
        if (resolvedRetryTimeout != Timeout.InfiniteTimeSpan && resolvedRetryTimeout < firstRetryInterval)
            throw new ArgumentOutOfRangeException(nameof(retryTimeout), retryTimeout,
                $"The retry timeout value must be greater than or equal to the first retry interval value of {firstRetryInterval}");

        this.MaxNumberOfAttempts = maxNumberOfAttempts;
        this.FirstRetryInterval = firstRetryInterval;
        this.BackoffCoefficient = backoffCoefficient;
        this.MaxRetryInterval = maxRetryInterval;
        this.RetryTimeout = resolvedRetryTimeout;
    }

    /// <summary>
    /// Gets the max number of attempts for executing a given task.
    /// </summary>
    public int MaxNumberOfAttempts { get; }

    /// <summary>
    /// Gets the amount of time to delay between the first and second attempt.
    /// </summary>
    public TimeSpan FirstRetryInterval { get; }

    /// <summary>
    /// Gets the exponential back-off coefficient used to determine the delay between subsequent retries.
    /// </summary>
    /// <value>
    /// Defaults to 1.0 for no back-off.
    /// </value>
    public double BackoffCoefficient { get; }

    /// <summary>
    /// Gets the maximum time to delay between attempts.
    /// </summary>
    /// <value>
    /// Defaults to 1 hour.
    /// </value>
    public TimeSpan? MaxRetryInterval { get; }

    /// <summary>
    /// Gets the overall timeout for retries. No further attempts will be made at executing a task after this retry
    /// timeout expires.
    /// </summary>
    /// <value>
    /// Defaults to <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </value>
    public TimeSpan RetryTimeout { get; }

    /// <summary>
    /// Calculates the next retry delay based on the attempt number.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-indexed).</param>
    /// <returns>The delay to wait before the next retry attempt.</returns>
    internal TimeSpan GetNextDelay(int attemptNumber)
    {
        if (attemptNumber <= 0)
            return TimeSpan.Zero;
        
        // Calculate: firstRetryInterval * (backoffCoefficient ^ (attemptNumber - 1))
        var nextDelayInMilliseconds = this.FirstRetryInterval.TotalMilliseconds *
                                      Math.Pow(this.BackoffCoefficient, attemptNumber - 1);

        var nextDelay = TimeSpan.FromMilliseconds(nextDelayInMilliseconds);

        // Cap at max retry interval
        return this.MaxRetryInterval is null ? nextDelay :
            nextDelay > this.MaxRetryInterval ? (TimeSpan)this.MaxRetryInterval : nextDelay;
    }
}
