// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Dapr.Jobs.Models.Responses;

namespace Dapr.Jobs;

/// <summary>
/// <para>
/// Defines client operations for managing Dapr jobs.
/// Use <see cref="DaprJobsClientBuilder"/> to create a <see cref="DaprJobsClient"/> or register
/// for use with dependency injection via
/// <see><cref>DaprJobsServiceCollectionExtensions.AddDaprJobsClient</cref></see>.
/// </para>
/// <para>
/// Implementations of <see cref="DaprJobsClient"/> implement <see cref="IDisposable"/> because the
/// client accesses network resources. For best performance, create a single long-lived client instance
/// and share it for the lifetime of the application. This is done for you if created via the DI extensions. Avoid
/// creating a disposing a client instance for each operation that the application performs - this can lead to socket
/// exhaustion and other problems.
/// </para>
/// </summary>
public abstract class DaprJobsClient : IDisposable
{
    private bool disposed;

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="cronExpression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task ScheduleCronJobAsync(string jobName, string cronExpression,
        DateTimeOffset? startingFrom = null, int? repeats = null, DateTimeOffset? ttl = null, ReadOnlyMemory<byte>? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a recurring job with an optional future starting date.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="interval">The interval at which the job should be triggered.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional maximum number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and StartingFrom are set, TTL needs to represent a later point in time.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task ScheduleIntervalJobAsync(string jobName, TimeSpan interval,
        DateTimeOffset? startingFrom = null, int? repeats = null, DateTimeOffset? ttl = null, ReadOnlyMemory<byte>? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a one-time job.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="scheduledTime">The point in time when the job should be run.</param>
    /// <param name="payload">Stores the main payload of the job which is passed to the trigger function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task ScheduleOneTimeJobAsync(string jobName, DateTimeOffset scheduledTime,
        ReadOnlyMemory<byte>? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the details of a registered job.
    /// </summary>
    /// <param name="jobName">The jobName of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The details comprising the job.</returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task<JobDetails> GetJobAsync(string jobName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes the specified job.
    /// </summary>
    /// <param name="jobName">The jobName of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task DeleteJobAsync(string jobName, CancellationToken cancellationToken = default);
    
    internal static KeyValuePair<string, string>? GetDaprApiTokenHeader(string apiToken)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return null;
        }

        return new KeyValuePair<string, string>("dapr-api-token", apiToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.disposed)
        {
            Dispose(disposing: true);
            this.disposed = true;
        }
    }

    /// <summary>
    /// Disposes the resources associated with the object.
    /// </summary>
    /// <param name="disposing"><c>true</c> if called by a call to the <c>Dispose</c> method; otherwise false.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
