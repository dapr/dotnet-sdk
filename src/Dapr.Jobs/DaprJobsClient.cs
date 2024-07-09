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

using System.Text.Json;
using Dapr.Jobs.Models.Responses;
using Dapr.Scheduler.Autogen.Grpc.v1;
using Google.Protobuf;

namespace Dapr.Jobs;

/// <summary>
/// Defines client operations for managing Dapr jobs.
/// </summary>
public abstract class DaprJobsClient
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used for JSON serialization purposes.
    /// </summary>
    public abstract JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="cronExpression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <param name="dueTime">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task ScheduleJobAsync<T>(string jobName, string cronExpression, DateTime? dueTime,
        uint? repeats = null,  DateTime? ttl = null, T? payload = default, CancellationToken cancellationToken = default) where T : IMessage;

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
    public abstract Task ScheduleJobAsync<T>(string jobName, TimeSpan interval, DateTime? startingFrom,
        uint? repeats = null, DateTime? ttl = null, T? payload = default,
        CancellationToken cancellationToken = default) where T : IMessage;

    /// <summary>
    /// Schedules a one-time job.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="scheduledTime">The point in time when the job should be run.</param>
    /// <param name="payload">Stores the main payload of the job which is passed to the trigger function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task ScheduleJobAsync<T>(string jobName, DateTime scheduledTime, T? payload = default,
        CancellationToken cancellationToken = default) where T : IMessage;

    /// <summary>
    /// Retrieves the details of a registered job.
    /// </summary>
    /// <param name="jobName">The jobName of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The details comprising the job.</returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task<JobDetails<T>> GetJobAsync<T>(string jobName, CancellationToken cancellationToken = default)
        where T : IMessage, new();

    /// <summary>
    /// Deletes the specified job.
    /// </summary>
    /// <param name="jobName">The jobName of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task DeleteJobAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches for triggered jobs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public abstract Task<IAsyncEnumerable<WatchedJobDetails<T>>> WatchJobsAsync<T>(CancellationToken cancellationToken = default) where T : IMessage, new()

    internal static KeyValuePair<string, string>? GetDaprApiTokenHeader(string apiToken)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return null;
        }

        return new KeyValuePair<string, string>("dapr-api-token", apiToken);
    }
}
