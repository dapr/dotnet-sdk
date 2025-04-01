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

using System.Text;
using System.Text.Json;
using Dapr.Jobs.Models;

namespace Dapr.Jobs.Extensions;

/// <summary>
/// Provides helper extensions for performing serialization operations when scheduling one-time Cron jobs for the developer.
/// </summary>
public static class DaprJobsSerializationExtensions
{
    /// <summary>
    /// Default JSON serializer options.
    /// </summary>
    private static readonly JsonSerializerOptions defaultOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Schedules a job with Dapr.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="schedule">The schedule defining when the job will be triggered.</param>
    /// <param name="payload">The main payload of the job expressed as a JSON-serializable object.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="jsonSerializerOptions">Optional JSON serialization options.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleJobWithPayloadAsync(this DaprJobsClient client, string jobName, DaprJobSchedule schedule,
        object payload, DateTime? startingFrom = null, int? repeats = null, JsonSerializerOptions? jsonSerializerOptions = null, DateTimeOffset? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var serializerOptions = jsonSerializerOptions ?? defaultOptions;
        var payloadBytes =
            JsonSerializer.SerializeToUtf8Bytes(payload, serializerOptions);

        await client.ScheduleJobAsync(jobName, schedule, payloadBytes, startingFrom, repeats, ttl, cancellationToken);
    }

    /// <summary>
    /// Schedules a job with Dapr.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="schedule">The schedule defining when the job will be triggered.</param>
    /// <param name="payload">The main payload of the job expressed as a string.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleJobWithPayloadAsync(this DaprJobsClient client, string jobName, DaprJobSchedule schedule,
        string payload, DateTime? startingFrom = null, int? repeats = null, DateTimeOffset? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        await client.ScheduleJobAsync(jobName, schedule, payloadBytes, startingFrom, repeats, ttl, cancellationToken);
    }
}
