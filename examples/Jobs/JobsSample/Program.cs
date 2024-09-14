#pragma warning disable CS0618 // Type or member is obsolete
using System.Text;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapDaprScheduledJob("myJob", (ILogger logger, JobDetails details) =>
{
    logger.LogInformation("Received trigger invocation for 'myJob'");

    if (details.Payload is not null)
    {
        var deserializedPayload = Encoding.UTF8.GetString(details.Payload.Value.ToArray());
        
        logger.LogInformation($"Received invocation for the 'myJob' job with payload '{deserializedPayload}'");
        return;
    }
    logger.LogInformation("Failed to deserialize payload for trigger invocation");
});

app.Run();

await using var scope = app.Services.CreateAsyncScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

logger.LogInformation("Scheduling one-time job 'myJob' to execute 10 seconds from now");
await daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(10)),
    Encoding.UTF8.GetBytes("This is a test"));
logger.LogInformation("Scheduled one-time job 'myJob'");


#pragma warning restore CS0618 // Type or member is obsolete
