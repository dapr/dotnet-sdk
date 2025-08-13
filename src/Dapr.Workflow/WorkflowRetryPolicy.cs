// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using System;
using System.Threading;
using Dapr.DurableTask;

namespace Dapr.Workflow;

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
public class WorkflowRetryPolicy(
    int maxNumberOfAttempts,
    TimeSpan firstRetryInterval,
    double backoffCoefficient = 1.0,
    TimeSpan? maxRetryInterval = null,
    TimeSpan? retryTimeout = null)
{
    private readonly RetryPolicy durableRetryPolicy = new(
        maxNumberOfAttempts,
        firstRetryInterval,
        backoffCoefficient,
        maxRetryInterval,
        retryTimeout);

    /// <summary>
    /// Gets the max number of attempts for executing a given task.
    /// </summary>
    public int MaxNumberOfAttempts => this.durableRetryPolicy.MaxNumberOfAttempts;

    /// <summary>
    /// Gets the amount of time to delay between the first and second attempt.
    /// </summary>
    public TimeSpan FirstRetryInterval => this.durableRetryPolicy.FirstRetryInterval;

    /// <summary>
    /// Gets the exponential back-off coefficient used to determine the delay between subsequent retries.
    /// </summary>
    /// <value>
    /// Defaults to 1.0 for no back-off.
    /// </value>
    public double BackoffCoefficient => this.durableRetryPolicy.BackoffCoefficient;

    /// <summary>
    /// Gets the maximum time to delay between attempts.
    /// </summary>
    /// <value>
    /// Defaults to 1 hour.
    /// </value>
    public TimeSpan MaxRetryInterval => this.durableRetryPolicy.MaxRetryInterval;

    /// <summary>
    /// Gets the overall timeout for retries. No further attempts will be made at executing a task after this retry
    /// timeout expires.
    /// </summary>
    /// <value>
    /// Defaults to <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </value>
    public TimeSpan RetryTimeout => this.durableRetryPolicy.RetryTimeout;

    internal RetryPolicy GetDurableRetryPolicy() => this.durableRetryPolicy;
}
