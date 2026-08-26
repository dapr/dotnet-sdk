#nowarn "FS3261"

namespace Dapr.IntegrationTest.FSharp.Jobs

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Dapr.Jobs
open Dapr.Jobs.Extensions
open Dapr.Jobs.Models
open Dapr.Testcontainers
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Harnesses
open global.Dapr.Testcontainers.Xunit.Attributes
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open global.Xunit

type JobsTests() =
    [<MinimumDaprRuntimeFact("1.16")>]
    member _.ShouldScheduleAndReceiveJob() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-jobs-component")
        let jobName = $"e2e-fs-job-{Guid.NewGuid():N}"
        let invocationTcs = TaskCompletionSource<(string * string)>(TaskCreationOptions.RunContinuationsAsynchronously)

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness = DaprHarnessBuilder(componentsDir).WithEnvironment(env).BuildJobs()
        use! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprJobsClient(
                        Action<IServiceProvider, DaprJobsClientBuilder>(
                            fun sp clientBuilder ->
                                let config = sp.GetRequiredService<IConfiguration>()
                                let grpcEndpoint = config["DAPR_GRPC_ENDPOINT"]
                                let httpEndpoint = config["DAPR_HTTP_ENDPOINT"]
                                if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                    clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore
                                if not (String.IsNullOrEmpty(httpEndpoint)) then
                                    clientBuilder.UseHttpEndpoint(httpEndpoint) |> ignore))
                    |> ignore)
                .ConfigureApp(fun app ->
                    app.MapDaprScheduledJobHandler(
                        Func<string, ReadOnlyMemory<byte>, ILogger<JobsTests>, CancellationToken, Task>(
                            fun incomingJobName payload logger _ ->
                                if not (isNull logger) then
                                    logger.LogInformation("Received job {Job}", incomingJobName)
                                invocationTcs.TrySetResult((Encoding.UTF8.GetString(payload.Span), incomingJobName))
                                |> ignore
                                Task.CompletedTask))
                    |> ignore)
                .BuildAndStartAsync()

        use scope = testApp.CreateScope()
        let daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>()

        let payload = Encoding.UTF8.GetBytes("Hello!")
        do! daprJobsClient.ScheduleJobAsync(
            jobName,
            DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2.0)),
            payload = ReadOnlyMemory(payload),
            repeats = Nullable(1),
            overwrite = true,
            cancellationToken = TestContext.Current.CancellationToken
        )

        let! (receivedPayload, receivedJobName) =
            invocationTcs.Task.WaitAsync(TimeSpan.FromSeconds(30.0), TestContext.Current.CancellationToken)

        Assert.Equal(Encoding.UTF8.GetString(payload), receivedPayload)
        Assert.Equal(jobName, receivedJobName)
    }
