#nowarn "FS3261" "57"
open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Dapr.DistributedLock
open Dapr.DistributedLock.Extensions

let builder = Host.CreateDefaultBuilder()

builder.ConfigureServices(fun services ->
    services.AddDaprDistributedLock() |> ignore
    services.AddLogging() |> ignore) |> ignore

let app = builder.Build()

let scope = app.Services.CreateScope()
let lockClient = scope.ServiceProvider.GetRequiredService<DaprDistributedLockClient>()

printfn "Attempting to lock myFile.txt..."

let lockTask = lockClient.TryLockAsync("redislock", "myFile.txt", "myApp", 60)

let fileLock = lockTask.Result
if not (obj.ReferenceEquals(fileLock, null)) then
    printfn "Successfully locked file"
    Task.Delay(TimeSpan.FromSeconds(3.0)).Wait()
    printfn "Releasing lock..."
    fileLock.DisposeAsync().AsTask().Wait()
else
    printfn "Failed to acquire lock"

app.Dispose()