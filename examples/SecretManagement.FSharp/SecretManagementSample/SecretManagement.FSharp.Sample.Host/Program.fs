#nowarn "FS3261"

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Dapr.SecretsManagement
open Dapr.SecretsManagement.Extensions
open SecretManagement.FSharp.Sample

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

builder.Services.AddDaprSecretsManagementClient()
    .AddMyVaultSecrets() |> ignore

let app: WebApplication = builder.Build()

app.MapGet("/secrets/{storeName}/{key}", Func<string, string, DaprSecretsManagementClient, CancellationToken, Task<IResult>>(fun storeName key secretsClient ct ->
    task {
        let! secret = secretsClient.GetSecretAsync(storeName, key, cancellationToken = ct)
        return Results.Ok(secret)
    })) |> ignore

app.MapGet("/secrets/{storeName}", Func<string, DaprSecretsManagementClient, CancellationToken, Task<IResult>>(fun storeName secretsClient ct ->
    task {
        let! secrets = secretsClient.GetBulkSecretAsync(storeName, cancellationToken = ct)
        return Results.Ok(secrets)
    })) |> ignore

app.MapGet("/typed-secrets", Func<IMyVaultSecrets, IResult>(fun secrets ->
    Results.Ok({| DatabaseConnection = secrets.DatabaseConnection; ApiKey = secrets.ApiKey |}))) |> ignore

app.Run()
