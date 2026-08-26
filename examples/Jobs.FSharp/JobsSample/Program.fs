#nowarn "FS3261"
open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Dapr.Jobs
open Dapr.Jobs.Extensions
open Dapr.Jobs.Models

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()
builder.Services.AddDaprJobsClient() |> ignore
let app: WebApplication = builder.Build()

app.MapDaprScheduledJobHandler(Func<string, ReadOnlyMemory<byte>, IServiceProvider, CancellationToken, Task>(fun jobName payload _ _ ->
    task {
        let deserialized = Encoding.UTF8.GetString(payload.Span)
        printfn "Received trigger for job '%s' with payload '%s'" jobName deserialized
    })) |> ignore

let jobsClient = app.Services.GetRequiredService<DaprJobsClient>()

let schedule = DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(10.0))
let payloadBytes = ReadOnlyMemory(Encoding.UTF8.GetBytes("Hello from F#"))
jobsClient.ScheduleJobAsync("fsharp-sample-job", schedule, payloadBytes) |> ignore

printfn "Job scheduled. Waiting for triggers..."

app.Run()