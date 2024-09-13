using System.Text;
using System.Text.Json;

namespace Dapr.Jobs.Extensions.Helpers.Serialization;

/// <summary>
/// Provides helper extensions for performing serialization operations when scheduling interface jobs for the developer.
/// </summary>
public static class DaprIntervalJobsSerializationExtensions
{
    /// <summary>
    /// Schedules a recurring job with an optional future starting date.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="interval">The interval at which the job should be triggered.</param>
    /// <param name="payload">The main payload of the job expressed as a JSON-serializable object.</param>
    /// <param name="jsonSerializerOptions">Optional options to use for the <see cref="JsonSerializer"/>.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional maximum number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and StartingFrom are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleIntervalJobWithPayloadAsync(this DaprJobsClient client, string jobName,
        TimeSpan interval, object payload, JsonSerializerOptions? jsonSerializerOptions = null,
        DateTimeOffset? startingFrom = null,
        int? repeats = null, DateTimeOffset? ttl = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var serializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var payloadBytes =
            JsonSerializer.SerializeToUtf8Bytes(payload, serializerOptions);

        await client.ScheduleIntervalJobAsync(jobName, interval, startingFrom, repeats, ttl, payloadBytes,
            cancellationToken);
    }

    /// <summary>
    /// Schedules a recurring job with an optional future starting date.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="interval">The interval at which the job should be triggered.</param>
    /// <param name="payload">The main payload of the job expressed as a JSON-serializable object.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional maximum number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and StartingFrom are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleIntervalJobWithPayloadAsync(this DaprJobsClient client, string jobName,
        TimeSpan interval, string payload, DateTimeOffset? startingFrom = null,
        int? repeats = null, DateTimeOffset? ttl = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
            JsonSerializer.SerializeToUtf8Bytes(payload);
        await client.ScheduleIntervalJobAsync(jobName, interval, startingFrom, repeats, ttl, payloadBytes,
            cancellationToken);
    }
}
