---
type: docs
title: "How to: Author and manage Dapr Jobs in the .NET SDK"
linkTitle: "How to: Author & manage jobs"
weight: 51000
description: Learn how to author and manage Dapr Jobs using the .NET SDK
---

Let's create an endpoint that will be invoked by Dapr Jobs when it triggers, then schedule the job in the same app. We'll use the [simple example provided here](https://github.com/dapr/dotnet-sdk/tree/master/examples/Jobs), for the following demonstration and walk through it as an explainer of how you can schedule one-time or recurring jobs using either an interval or Cron expression yourself. In this guide,
you will:

- Deploy a .NET Web API application ([JobsSample](https://github.com/dapr/dotnet-sdk/tree/master/examples/Jobs/JobsSample))
- Utilize the Dapr .NET Jobs SDK to schedule a job invocation and set up the endpoint to be triggered

In the .NET example project:
- The main [`Program.cs`](https://github.com/dapr/dotnet-sdk/tree/master/examples/Jobs/JobsSample/Program.cs) file comprises the entirety of this demonstration.

## Prerequisites
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost)
- [.NET 6](https://dotnet.microsoft.com/download/dotnet/6.0), [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) or [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) installed
- [Dapr.Jobs](https://www.nuget.org/packages/Dapr.Jobs) NuGet package installed to your project

{{% alert title="Note" color="primary" %}}

Note that while .NET 6 is the minimum support version of .NET in Dapr v1.15, only .NET 8 and .NET 9 will continue to be supported by Dapr in v1.16 and later.

{{% /alert %}}

## Set up the environment
Clone the [.NET SDK repo](https://github.com/dapr/dotnet-sdk).

```sh
git clone https://github.com/dapr/dotnet-sdk.git
```

From the .NET SDK root directory, navigate to the Dapr Jobs example.

```sh
cd examples/Jobs
```

## Run the application locally

To run the Dapr application, you need to start the .NET program and a Dapr sidecar. Navigate to the `JobsSample` directory.

```sh
cd JobsSample
```

We'll run a command that starts both the Dapr sidecar and the .NET program at the same time.

```sh
dapr run --app-id jobsapp --dapr-grpc-port 4001 --dapr-http-port 3500 -- dotnet run
```

> Dapr listens for HTTP requests at `http://localhost:3500` and internal Jobs gRPC requests at `http://localhost:4001`.

## Register the Dapr Jobs client with dependency injection
The Dapr Jobs SDK provides an extension method to simplify the registration of the Dapr Jobs client. Before completing 
the dependency injection registration in `Program.cs`, add the following line:

```cs
var builder = WebApplication.CreateBuilder(args);

//Add anywhere between these two lines
builder.Services.AddDaprJobsClient();

var app = builder.Build();
```

> Note that in today's implementation of the Jobs API, the app that schedules the job will also be the app that receives the trigger notification. In other words, you cannot schedule a trigger to run in another application. As a result, while you don't explicitly need the Dapr Jobs client to be registered in your application to schedule a trigger invocation endpoint, your endpoint will never be invoked without the same app also scheduling the job somehow (whether via this Dapr Jobs .NET SDK or an HTTP call to the sidecar).

It's possible that you may want to provide some configuration options to the Dapr Jobs client that
should be present with each call to the sidecar such as a Dapr API token, or you want to use a non-standard
HTTP or gRPC endpoint. This is possible through use of an overload of the registration method that allows configuration of a 
`DaprJobsClientBuilder` instance:

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient((_, daprJobsClientBuilder) =>
{
    daprJobsClientBuilder.UseDaprApiToken("abc123");
    daprJobsClientBuilder.UseHttpEndpoint("http://localhost:8512"); //Non-standard sidecar HTTP endpoint
});

var app = builder.Build();
```

Still, it's possible that whatever values you wish to inject need to be retrieved from some other source, itself registered as a dependency. There's one more overload you can use to inject an `IServiceProvider` into the configuration action method. In the following example, we register a fictional singleton that can retrieve secrets from somewhere and pass it into the configuration method for `AddDaprJobClient` so
we can retrieve our Dapr API token from somewhere else for registration here:

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SecretRetriever>();
builder.Services.AddDaprJobsClient((serviceProvider, daprJobsClientBuilder) =>
{
    var secretRetriever = serviceProvider.GetRequiredService<SecretRetriever>();
    var daprApiToken = secretRetriever.GetSecret("DaprApiToken").Value;
    daprJobsClientBuilder.UseDaprApiToken(daprApiToken);

    daprJobsClientBuilder.UseHttpEndpoint("http://localhost:8512");
});

var app = builder.Build();
```

## Use the Dapr Jobs client using IConfiguration
It's possible to configure the Dapr Jobs client using the values in your registered `IConfiguration` as well without
explicitly specifying each of the value overrides using the `DaprJobsClientBuilder` as demonstrated in the previous
section. Rather, by populating an `IConfiguration` made available through dependency injection the `AddDaprJobsClient()`
registration will automatically use these values over their respective defaults.

Start by populating the values in your configuration. This can be done in several different ways as demonstrated below.

### Configuration via `ConfigurationBuilder`
Application settings can be configured without using a configuration source and by instead populating the value in-memory
using a `ConfigurationBuilder` instance:

```csharp
var builder = WebApplication.CreateBuilder();

//Create the configuration
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string> {
            { "DAPR_HTTP_ENDPOINT", "http://localhost:54321" },
            { "DAPR_API_TOKEN", "abc123" }
        })
    .Build();

builder.Configuration.AddConfiguration(configuration);
builder.Services.AddDaprJobsClient(); //This will automatically populate the HTTP endpoint and API token values from the IConfiguration
```

### Configuration via Environment Variables
Application settings can be accessed from environment variables available to your application.

The following environment variables will be used to populate both the HTTP endpoint and API token used to register the
Dapr Jobs client.

| Key | Value |
| --- | --- |
| DAPR_HTTP_ENDPOINT | http://localhost:54321 |
| DAPR_API_TOKEN | abc123 |

```csharp
var builder = WebApplication.CreateBuilder();

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddDaprJobsClient();
```

The Dapr Jobs client will be configured to use both the HTTP endpoint `http://localhost:54321` and populate all outbound
requests with the API token header `abc123`.

### Configuration via prefixed Environment Variables

However, in shared-host scenarios where there are multiple applications all running on the same machine without using
containers or in development environments, it's not uncommon to prefix environment variables. The following example
assumes that both the HTTP endpoint and the API token will be pulled from environment variables prefixed with the
value "myapp_". The two environment variables used in this scenario are as follows:

| Key | Value |
| --- | --- |
| myapp_DAPR_HTTP_ENDPOINT | http://localhost:54321 |
| myapp_DAPR_API_TOKEN | abc123 |

These environment variables will be loaded into the registered configuration in the following example and made available
without the prefix attached.

```csharp
var builder = WebApplication.CreateBuilder();

builder.Configuration.AddEnvironmentVariables(prefix: "myapp_");
builder.Services.AddDaprJobsClient();
```

The Dapr Jobs client will be configured to use both the HTTP endpoint `http://localhost:54321` and populate all outbound
requests with the API token header `abc123`.

## Use the Dapr Jobs client without relying on dependency injection
While the use of dependency injection simplifies the use of complex types in .NET and makes it easier to
deal with complicated configurations, you're not required to register the `DaprJobsClient` in this way. Rather, you can also elect to create an instance of it from a `DaprJobsClientBuilder` instance as demonstrated below:

```cs

public class MySampleClass
{
    public void DoSomething()
    {
        var daprJobsClientBuilder = new DaprJobsClientBuilder();
        var daprJobsClient = daprJobsClientBuilder.Build();

        //Do something with the `daprJobsClient`
    }
}
```

## Set up a endpoint to be invoked when the job is triggered

It's easy to set up a jobs endpoint if you're at all familiar with [minimal APIs in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview) as the syntax is the same between the two.

Once dependency injection registration has been completed, configure the application the same way you would to handle mapping an HTTP request via the minimal API functionality in ASP.NET Core. Implemented as an extension method,
pass the name of the job it should be responsive to and a delegate. Services can be injected into the delegate's arguments as you wish and the job payload can be accessed from the `ReadOnlyMemory<byte>` originally provided to the 
job registration.

There are two delegates you can use here. One provides an `IServiceProvider` in case you need to inject other services into the handler:

```cs
//We have this from the example above
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient();

var app = builder.Build();

//Add our endpoint registration
app.MapDaprScheduledJob("myJob", (IServiceProvider serviceProvider, string jobName, ReadOnlyMemory<byte> jobPayload) => {
    var logger = serviceProvider.GetService<ILogger>();
    logger?.LogInformation("Received trigger invocation for '{jobName}'", "myJob");

    //Do something...
});

app.Run();
```

The other overload of the delegate doesn't require an `IServiceProvider` if not necessary:

```cs
//We have this from the example above
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprJobsClient();

var app = builder.Build();

//Add our endpoint registration
app.MapDaprScheduledJob("myJob", (string jobName, ReadOnlyMemory<byte> jobPayload) => {
    //Do something...
});

app.Run();
```

## Register the job

Finally, we have to register the job we want scheduled. Note that from here, all SDK methods have cancellation token support and use a default token if not otherwise set.

There are three different ways to set up a job that vary based on how you want to configure the schedule:

### One-time job
A one-time job is exactly that; it will run at a single point in time and will not repeat. This approach requires that you select a job name and specify a time it should be triggered.

| Argument Name | Type | Description | Required |
|---|---|---|---|
| jobName | string | The name of the job being scheduled. | Yes |
| scheduledTime | DateTime | The point in time when the job should be run. | Yes |
| payload | ReadOnlyMemory<byte> | Job data provided to the invocation endpoint when triggered. | No |
| cancellationToken | CancellationToken | Used to cancel out of the operation early, e.g. because of an operation timeout. | No |

One-time jobs can be scheduled from the Dapr Jobs client as in the following example:

```cs
public class MyOperation(DaprJobsClient daprJobsClient)
{
    public async Task ScheduleOneTimeJobAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var threeDaysFromNow = today.AddDays(3);

        await daprJobsClient.ScheduleOneTimeJobAsync("myJobName", threeDaysFromNow, cancellationToken: cancellationToken);
    }
}
```

### Interval-based job
An interval-based job is one that runs on a recurring loop configured as a fixed amount of time, not unlike how [reminders](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-timers-reminders/#actor-reminders) work in the Actors building block today. These jobs can be scheduled with a number of optional arguments as well:

| Argument Name | Type | Description | Required |
|---|---|---|---|
| jobName | string | The name of the job being scheduled. | Yes |
| interval | TimeSpan | The interval at which the job should be triggered. | Yes |
| startingFrom | DateTime | The point in time from which the job schedule should start. | No |
| repeats | int | The maximum number of times the job should be triggered. | No |
| ttl | When the job should expires and no longer trigger. | No |
| payload | ReadOnlyMemory<byte> | Job data provided to the invocation endpoint when triggered. | No |
| cancellationToken | CancellationToken | Used to cancel out of the operation early, e.g. because of an operation timeout. | No |

Interval-based jobs can be scheduled from the Dapr Jobs client as in the following example:

```cs
public class MyOperation(DaprJobsClient daprJobsClient)
{

    public async Task ScheduleIntervalJobAsync(CancellationToken cancellationToken)
    {
        var hourlyInterval = TimeSpan.FromHours(1);

        //Trigger the job hourly, but a maximum of 5 times
        await daprJobsClient.ScheduleIntervalJobAsync("myJobName", hourlyInterval, repeats: 5), cancellationToken: cancellationToken;
    }
}
```

### Cron-based job
A Cron-based job is scheduled using a Cron expression. This gives more calendar-based control over when the job is triggered as it can used calendar-based values in the expression.  Like the other options, these jobs can be scheduled with a number of optional arguments as well:

| Argument Name | Type | Description | Required |
|---|---|---|---|
| jobName | string | The name of the job being scheduled. | Yes |
| cronExpression | string | The systemd Cron-like expression indicating when the job should be triggered. | Yes |
| startingFrom | DateTime | The point in time from which the job schedule should start. | No |
| repeats | int | The maximum number of times the job should be triggered. | No |
| ttl | When the job should expires and no longer trigger. | No |
| payload | ReadOnlyMemory<byte> | Job data provided to the invocation endpoint when triggered. | No |
| cancellationToken | CancellationToken | Used to cancel out of the operation early, e.g. because of an operation timeout. | No |

A Cron-based job can be scheduled from the Dapr Jobs client as follows:

```cs
public class MyOperation(DaprJobsClient daprJobsClient)
{
    public async Task ScheduleCronJobAsync(CancellationToken cancellationToken)
    {
        //At the top of every other hour on the fifth day of the month
        const string cronSchedule = "0 */2 5 * *";

        //Don't start this until next month
        var now = DateTime.UtcNow;
        var oneMonthFromNow = now.AddMonths(1);
        var firstOfNextMonth = new DateTime(oneMonthFromNow.Year, oneMonthFromNow.Month, 1, 0, 0, 0);

        //Trigger the job hourly, but a maximum of 5 times
        await daprJobsClient.ScheduleCronJobAsync("myJobName", cronSchedule, dueTime: firstOfNextMonth, cancellationToken: cancellationToken);
    }
}
```

## Get details of already-scheduled job
If you know the name of an already-scheduled job, you can retrieve its metadata without waiting for it to
be triggered. The returned `JobDetails` exposes a few helpful properties for consuming the information from the Dapr Jobs API:

- If the `Schedule` property contains a Cron expression, the `IsCronExpression` property will be true and the expression will also be available in the `CronExpression` property.
- If the `Schedule` property contains a duration value, the `IsIntervalExpression` property will instead be true and the value will be converted to a `TimeSpan` value accessible from the `Interval` property.

This can be done by using the following:

```cs
public class MyOperation(DaprJobsClient daprJobsClient)
{
    public async Task<JobDetails> GetJobDetailsAsync(string jobName, CancellationToken cancellationToken)
    {
        var jobDetails = await daprJobsClient.GetJobAsync(jobName, canecllationToken);
        return jobDetails;
    }
}
```

## Delete a scheduled job
To delete a scheduled job, you'll need to know its name. From there, it's as simple as calling the `DeleteJobAsync` method on the Dapr Jobs client:

```cs
public class MyOperation(DaprJobsClient daprJobsClient)
{
    public async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken)
    {
        await daprJobsClient.DeleteJobAsync(jobName, cancellationToken);
    }
}
```