#pragma warning disable CS0618 // Type or member is obsolete
using System.Text;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient();

var app = builder.Build();

//Set a handler to deal with incoming jobs
var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
app.MapDaprScheduledJobHandler((string? jobName, DaprJobDetails? jobDetails, ILogger? logger, CancellationToken cancellationToken) =>
{
    logger?.LogInformation("Received trigger invocation for job '{jobName}'", jobName);
    if (jobDetails?.Payload is not null)
    {
        var deserializedPayload = Encoding.UTF8.GetString(jobDetails.Payload);
        logger?.LogInformation("Received invocation for the job '{jobName}' with payload '{deserializedPayload}'",
            jobName, deserializedPayload);
        //Do something that needs the cancellation token
    }
    else
    {
        logger?.LogWarning("Failed to deserialize payload for job '{jobName}'", jobName);
    }
    return Task.CompletedTask;
}, cancellationTokenSource.Token);

app.Run();

await using var scope = app.Services.CreateAsyncScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

logger.LogInformation("Scheduling one-time job 'myJob' to execute 10 seconds from now");
await daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(10)),
    Encoding.UTF8.GetBytes("This is a test"));
logger.LogInformation("Scheduled one-time job 'myJob'");


#pragma warning restore CS0618 // Type or member is obsolete
