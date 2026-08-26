#nowarn "FS3261" "FS3264" "FS3265"
namespace Dapr.Actors.Next.Examples.DeviceManagement.Tests

open System
open System.Reflection
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Examples.DeviceManagement
open Dapr.Actors.Next.Interpreted
open Dapr.Actors.Next.Testing
open Microsoft.Extensions.DependencyInjection
open Xunit

type DeviceManagementTests() =
    static let frontDoor = ActorId.Create("front-door")

    static member private CreateRuntime(store: IInterpretedMachineStore) : ActorTestRuntime =
        ActorTestRuntime(fun services ->
            let deviceTypes = DeviceTypeRegistry()
            deviceTypes.AddDeviceType(DeviceManagementDemo.SmartLockDeviceType)
            services.AddSingleton<IActorRegistry>(deviceTypes) |> ignore
            services.AddSingleton(store) |> ignore
            services.AddSingleton<ICapabilityRegistry, DeviceCapabilityRegistry>() |> ignore
            services.AddDaprInterpretedActors(DeviceManagementDemo.SmartLockDeviceType) |> ignore)

    static member private GetProvider(runtime: ActorTestRuntime) : IServiceProvider =
        runtime.GetType().GetField("provider", BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue(runtime) :?> IServiceProvider

    static member private State(result: string) : string =
        use doc = JsonDocument.Parse(result)
        doc.RootElement.GetProperty("State").GetString()

    static member private Deploy(store: IInterpretedMachineStore, definition: InterpretedMachineDefinition) : Task =
        let deployer = InterpretedMachineDeployer(
            InterpretedMachineVerifier(DeviceCapabilityRegistry()),
            store)
        deployer.DeployAsync("SmartLock", frontDoor, definition).AsTask()

    [<Fact>]
    member this.Control_plane_lists_device_type_and_sends_command_without_contract() : Task = task {
        let store = InMemoryInterpretedMachineStore()
        do! DeviceManagementTests.Deploy(store, DeviceManagementDemo.SmartLockDefault())
        use runtime = DeviceManagementTests.CreateRuntime(store)
        let provider = DeviceManagementTests.GetProvider(runtime)
        let controller = DeviceControlPlane(
            provider.GetRequiredService<IActorRegistry>(),
            provider.GetRequiredService<IDynamicActorClient>())

        Assert.Contains("SmartLock.Raise(InterpretedEvent)", controller.ListDeviceOperations())

        let unlock = controller.SendCommandAsync("SmartLock", frontDoor.Value, "Unlock")
        do! runtime.RunToIdle()
        let! unlocked = unlock
        Assert.Equal("Unlocking", DeviceManagementTests.State(unlocked))

        let stopped = controller.RecordSensorEventAsync("SmartLock", frontDoor.Value, "MotorStopped")
        do! runtime.RunToIdle()
        let! stoppedResult = stopped
        Assert.Equal("Unlocked", DeviceManagementTests.State(stoppedResult))
    }

    [<Fact>]
    member this.Verification_rejects_device_type_that_can_strand_a_lock() : unit =
        let verifier = InterpretedMachineVerifier(DeviceCapabilityRegistry())
        let stranded = DeviceManagementDemo.SmartLockDefinition false "ActuateMotor"
        let result = DeviceManagementDemo.Verify verifier stranded
        Assert.False(result.IsValid)
        Assert.Contains(result.Defects, fun d -> d.Contains("State 'Unlocking' is a dead end", StringComparison.Ordinal))

    [<Fact>]
    member this.Well_formed_device_type_definition_passes_verification() : unit =
        let verifier = InterpretedMachineVerifier(DeviceCapabilityRegistry())
        let result = DeviceManagementDemo.Verify verifier (DeviceManagementDemo.SmartLockDefault())
        Assert.True(result.IsValid)
        result.ThrowIfInvalid()

    [<Fact>]
    member this.Definition_with_unregistered_effect_is_rejected_before_rollout() : Task = task {
        let registry = DeviceCapabilityRegistry()
        let verifier = InterpretedMachineVerifier(registry)
        let store = InMemoryInterpretedMachineStore()
        let deployer = InterpretedMachineDeployer(verifier, store)
        let definition = DeviceManagementDemo.SmartLockDefinition true "UnknownMotor"

        let ok, _ = (registry :> ICapabilityRegistry).TryGetEffect("UnknownMotor")
        Assert.False(ok)
        let! ex = Assert.ThrowsAsync<InvalidOperationException>(fun () -> deployer.DeployAsync("SmartLock", frontDoor, definition).AsTask())
        Assert.Contains("Effect 'UnknownMotor'", ex.Message, StringComparison.Ordinal)
        let! stored = store.GetAsync("SmartLock", frontDoor)
        Assert.Null(stored)
    }
