using System.Text;
using System.Text.Json;

namespace Dapr.Jobs.Extensions.Helpers.Serialization;

/// <summary>
/// Provides helper extensions for performing serialization operations when scheduling one-time Cron jobs for the developer.
/// </summary>
public static class DaprOneTimeJobsSerializationExtensions
{
    /// <summary>
    /// Schedules a one-time job.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="scheduledTime">The point in time when the job should be run.</param>
    /// <param name="payload">The main payload of the job expressed as a JSON-serializable object.</param>
    /// <param name="jsonSerializerOptions">Optional options to use for the <see cref="JsonSerializer"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleOneTimeJobWithPayloadAsync(this DaprJobsClient client, string jobName, DateTime scheduledTime,
        object payload, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var serializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var payloadBytes =
            JsonSerializer.SerializeToUtf8Bytes(payload, serializerOptions);

        await client.ScheduleOneTimeJobAsync(jobName, scheduledTime, payloadBytes, cancellationToken);
    }

    /// <summary>
    /// Schedules a one-time job.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="scheduledTime">The point in time when the job should be run.</param>
    /// <param name="payload">The main payload of the job expressed as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleOneTimeJobWithPayloadAsync(this DaprJobsClient client, string jobName, DateTime scheduledTime,
        string payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        await client.ScheduleOneTimeJobAsync(jobName, scheduledTime, payloadBytes, cancellationToken);
    }
}
