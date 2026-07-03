using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Examples.DeviceManagement;
using Dapr.Actors.Next.Interpreted;

var builder = WebApplication.CreateBuilder(args);
var deviceTypes = new DeviceTypeRegistry();
deviceTypes.AddDeviceType(DeviceManagementDemo.SmartLockDeviceType);
builder.Services.AddSingleton<IActorRegistry>(deviceTypes);
builder.Services.AddSingleton<ICapabilityRegistry, DeviceCapabilityRegistry>();
builder.Services.AddSingleton<DeviceControlPlane>();
builder.Services.AddDaprInterpretedActors(DeviceManagementDemo.SmartLockDeviceType);

var app = builder.Build();

app.MapGet("/", () => "Interpreted device sample. POST /devices/{deviceId}/onboard, then unlock, motor-stopped, lock, and motor-stopped.");

app.MapGet("/device-types", (IActorRegistry registry) =>
    registry.Actors.Select(actor => new DeviceTypeDescription(
        actor.ActorType,
        actor.Methods.Select(method => method.Name).Order(StringComparer.Ordinal).ToArray())));

app.MapGet("/device-types/{deviceType}/operations", (
    string deviceType,
    DeviceControlPlane controlPlane) =>
{
    var operations = controlPlane.ListDeviceOperations()
        .Where(operation => operation.StartsWith(deviceType + ".", StringComparison.Ordinal))
        .ToArray();
    return operations.Length == 0 ? Results.NotFound() : Results.Ok(operations);
});

app.MapPost("/devices/{deviceId}/onboard", async (
    string deviceId,
    InterpretedMachineDeployer deployer,
    CancellationToken cancellationToken) =>
{
    await deployer.DeployAsync(
        DeviceManagementDemo.SmartLockDeviceType,
        Dapr.Actors.Next.Abstractions.ActorId.Create(deviceId),
        DeviceManagementDemo.SmartLockDefinition(),
        cancellationToken);
    return Results.Ok(new DeviceOnboarded(DeviceManagementDemo.SmartLockDeviceType, deviceId, "Locked"));
});

app.MapPost("/devices/{deviceId}/commands/unlock", (
    string deviceId,
    DeviceControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.SendCommandAsync(DeviceManagementDemo.SmartLockDeviceType, deviceId, "Unlock", cancellationToken)));

app.MapPost("/devices/{deviceId}/commands/lock", (
    string deviceId,
    DeviceControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.SendCommandAsync(DeviceManagementDemo.SmartLockDeviceType, deviceId, "Lock", cancellationToken)));

app.MapPost("/devices/{deviceId}/sensors/motor-stopped", (
    string deviceId,
    DeviceControlPlane controlPlane,
    CancellationToken cancellationToken) =>
    ToJsonResult(controlPlane.RecordSensorEventAsync(DeviceManagementDemo.SmartLockDeviceType, deviceId, "MotorStopped", cancellationToken)));

await app.RunAsync();
return;

static async Task<IResult> ToJsonResult(Task<string?> invoke)
{
    var json = await invoke;
    return json is null ? Results.NoContent() : Results.Content(json, "application/json");
}

internal sealed record DeviceTypeDescription(string ActorType, IReadOnlyList<string> Operations);

internal sealed record DeviceOnboarded(string DeviceType, string DeviceId, string InitialState);
