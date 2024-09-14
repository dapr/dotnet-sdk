using System.Text;
using System.Text.Json;

namespace Dapr.Jobs.Extensions.Helpers.Serialization;

/// <summary>
/// Provides helper extensions for performing serialization operations when scheduling one-time Cron jobs for the developer.
/// </summary>
public static class DaprJobsSerializationExtensions
{
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

        var serializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
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
