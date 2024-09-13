using System.Text;
using System.Text.Json;
using ArgumentNullException = System.ArgumentNullException;

namespace Dapr.Jobs.Extensions.Helpers.Serialization;

/// <summary>
/// Provides helper extensions for performing serialization operations when scheduling Cron jobs for the developer.
/// </summary>
public static class DaprCronJobsSerializationExtensions
{
    private static readonly JsonSerializerOptions defaultOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="cronExpression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <param name="payload">The main payload of the job expressed as a JSON-serializable object.</param>
    /// <param name="jsonSerializerOptions">Optional options to use for the <see cref="JsonSerializer"/>.</param>
    /// <param name="dueTime">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleCronJobWithPayloadAsync(this DaprJobsClient client, string jobName,
        string cronExpression, object payload, JsonSerializerOptions? jsonSerializerOptions = null,
        DateTimeOffset? dueTime = null, int? repeats = null, DateTimeOffset? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var serializerOptions = jsonSerializerOptions ?? defaultOptions;
        var payloadBytes =
            JsonSerializer.SerializeToUtf8Bytes(payload, serializerOptions);

        await client.ScheduleCronJobAsync(jobName, cronExpression, dueTime, repeats, ttl, payloadBytes,
            cancellationToken);
    }

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <param name="client">The <see cref="DaprJobsClient"/> instance.</param>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="cronExpression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <param name="payload">The main payload of the job expressed as a string.</param>
    /// <param name="dueTime">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public static async Task ScheduleCronJobWithPayloadAsync(this DaprJobsClient client, string jobName,
        string cronExpression, string payload, DateTimeOffset? dueTime = null, int? repeats = null,
        DateTimeOffset? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload, nameof(payload));

        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        await client.ScheduleCronJobAsync(jobName, cronExpression, dueTime, repeats, ttl, payloadBytes,
            cancellationToken);
    }
}
