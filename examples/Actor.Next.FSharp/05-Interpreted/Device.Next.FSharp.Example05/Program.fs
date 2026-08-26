open System
open System.Collections.Generic
open System.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted
open Dapr.Actors.Next.Examples.DeviceManagement

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()

let deviceTypes = DeviceTypeRegistry()
deviceTypes.AddDeviceType(DeviceManagementDemo.SmartLockDeviceType)
builder.Services.AddSingleton<IActorRegistry>(deviceTypes) |> ignore
builder.Services.AddSingleton<ICapabilityRegistry, DeviceCapabilityRegistry>() |> ignore
builder.Services.AddSingleton<DeviceControlPlane>() |> ignore
builder.Services.AddDaprInterpretedActors(DeviceManagementDemo.SmartLockDeviceType) |> ignore

let app: WebApplication = builder.Build()

app.MapGet("/", Func<string>(fun () -> "Interpreted device sample. POST /devices/{deviceId}/onboard, then unlock, motor-stopped, lock, and motor-stopped.")) |> ignore

app.MapGet("/device-types", Func<IActorRegistry, DeviceTypeDescription list>(fun registry ->
    registry.Actors
            .Select(fun actor ->
                { ActorType = actor.ActorType
                  Operations = actor.Methods.Select(fun m -> m.Name).Order(StringComparer.Ordinal).ToArray() :> IReadOnlyList<string> })
            .ToList() :> seq<DeviceTypeDescription>
            |> Seq.toList)) |> ignore

app.MapPost("/devices/{deviceId}/onboard", Func<string, InterpretedMachineDeployer, CancellationToken, Task<IResult>>(fun deviceId deployer ct ->
    task {
        do! deployer.DeployAsync(
                DeviceManagementDemo.SmartLockDeviceType,
                ActorId.Create(deviceId),
                DeviceManagementDemo.SmartLockDefault(),
                ct)
        return Results.Ok({ DeviceType = DeviceManagementDemo.SmartLockDeviceType; DeviceId = deviceId; InitialState = "Locked" })
    })) |> ignore

app.MapPost("/devices/{deviceId}/commands/unlock", Func<string, DeviceControlPlane, CancellationToken, Task<IResult>>(fun deviceId controlPlane ct ->
    task {
        let! json = controlPlane.SendCommandAsync(DeviceManagementDemo.SmartLockDeviceType, deviceId, "Unlock", ct)
        return if obj.ReferenceEquals(json, null) then Results.NoContent() else Results.Content(json, "application/json")
    })) |> ignore

app.MapPost("/devices/{deviceId}/commands/lock", Func<string, DeviceControlPlane, CancellationToken, Task<IResult>>(fun deviceId controlPlane ct ->
    task {
        let! json = controlPlane.SendCommandAsync(DeviceManagementDemo.SmartLockDeviceType, deviceId, "Lock", ct)
        return if obj.ReferenceEquals(json, null) then Results.NoContent() else Results.Content(json, "application/json")
    })) |> ignore

app.MapPost("/devices/{deviceId}/sensors/motor-stopped", Func<string, DeviceControlPlane, CancellationToken, Task<IResult>>(fun deviceId controlPlane ct ->
    task {
        let! json = controlPlane.RecordSensorEventAsync(DeviceManagementDemo.SmartLockDeviceType, deviceId, "MotorStopped", ct)
        return if obj.ReferenceEquals(json, null) then Results.NoContent() else Results.Content(json, "application/json")
    })) |> ignore

app.Run()
