#nowarn "FS3261"
namespace Dapr.Actors.Next.Examples.DeviceManagement

open System
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Filters
open Dapr.Actors.Next.Abstractions.Registry
open Dapr.Actors.Next.Interpreted

type DeviceCommand = { Name: string }
type DeviceSensorEvent = { Name: string }
type DeviceTypeDescription = { ActorType: string; Operations: IReadOnlyList<string> }
type DeviceOnboarded = { DeviceType: string; DeviceId: string; InitialState: string }

module DeviceManagementDemo =
    let SmartLockDeviceType = "SmartLock"

    let SmartLockDefinition (includeUnlockingCompletion: bool) (motorEffect: string) : InterpretedMachineDefinition =
        let transitions = ResizeArray<InterpretedTransitionDefinition>()

        transitions.Add(InterpretedTransitionDefinition(
            Source = "Locked",
            Event = "Unlock",
            Branches = [|
                InterpretedBranchDefinition(
                    Guards = [ "CheckBattery" ],
                    Target = "Unlocking",
                    Effects = [ motorEffect ]
                )
            |]
        ))

        transitions.Add(InterpretedTransitionDefinition(
            Source = "Unlocked",
            Event = "Lock",
            Branches = [|
                InterpretedBranchDefinition(
                    Otherwise = true,
                    Target = "Locking",
                    Effects = [ motorEffect ]
                )
            |]
        ))

        transitions.Add(InterpretedTransitionDefinition(
            Source = "Locking",
            Event = "MotorStopped",
            Branches = [|
                InterpretedBranchDefinition(
                    Otherwise = true,
                    Target = "Locked"
                )
            |]
        ))

        if includeUnlockingCompletion then
            transitions.Add(InterpretedTransitionDefinition(
                Source = "Unlocking",
                Event = "MotorStopped",
                Branches = [|
                    InterpretedBranchDefinition(
                        Otherwise = true,
                        Target = "Unlocked"
                    )
                |]
            ))

        InterpretedMachineDefinition(
            DocumentVersion = 1,
            InitialState = "Locked",
            States = [|
                InterpretedStateDefinition(Name = "Locked")
                InterpretedStateDefinition(Name = "Unlocking")
                InterpretedStateDefinition(Name = "Unlocked")
                InterpretedStateDefinition(Name = "Locking")
            |],
            Transitions = transitions.ToArray()
        )

    let SmartLockDefault () = SmartLockDefinition true "ActuateMotor"

    let Verify (verifier: IInterpretedMachineVerifier) (definition: InterpretedMachineDefinition) : InterpretedMachineVerificationResult =
        verifier.Verify(definition)

type DeviceControlPlane(registry: IActorRegistry, client: IDynamicActorClient) =

    member _.ListDeviceOperations() : IReadOnlyList<string> =
        registry.Actors
            .SelectMany(fun actor ->
                actor.Methods.Select(fun method ->
                    let paramList = String.Join(", ", method.Parameters.Select(fun p -> p.ParameterType.Name))
                    actor.ActorType + "." + method.Name + "(" + paramList + ")"))
            .Order(StringComparer.Ordinal)
            .ToArray()

    member _.SendCommandAsync(deviceType: string, deviceId: string, command: string, ?cancellationToken: CancellationToken) : Task<string> =
        let ct = defaultArg cancellationToken CancellationToken.None
        let payload = { DeviceCommand.Name = command }
        let evt = InterpretedEvent(command, JsonSerializer.SerializeToElement(payload))
        client.InvokeAsync(deviceType, deviceId, "Raise", JsonSerializer.Serialize(evt), ct)

    member _.RecordSensorEventAsync(deviceType: string, deviceId: string, sensorEvent: string, ?cancellationToken: CancellationToken) : Task<string> =
        let ct = defaultArg cancellationToken CancellationToken.None
        let payload = { DeviceSensorEvent.Name = sensorEvent }
        let evt = InterpretedEvent(sensorEvent, JsonSerializer.SerializeToElement(payload))
        client.InvokeAsync(deviceType, deviceId, "Raise", JsonSerializer.Serialize(evt), ct)

type DeviceTypeRegistry() =
    let devices = Dictionary<string, ActorTypeDescriptor>(StringComparer.Ordinal)

    member _.AddDeviceType(deviceType: string) =
        devices.[deviceType] <- ActorTypeDescriptor(
            deviceType, 1,
            typeof<InterpretedStateMachineActor>,
            typeof<InterpretedStateMachineActor>,
            [
                ActorMethodDescriptor(
                    "Raise", "Raise",
                    typeof<InterpretedRaiseResult>,
                    [ ActorParameterDescriptor("evt", typeof<InterpretedEvent>, 0, false, false, null) ]
                )
            ])

    interface IActorRegistry with
        member _.Actors = devices.Values.OrderBy(fun d -> d.ActorType, StringComparer.Ordinal).ToArray() :> IReadOnlyList<ActorTypeDescriptor>
        member _.TryGet(actorType: string, [<Out>] descriptor: byref<ActorTypeDescriptor>) : bool =
            let ok, v = devices.TryGetValue(actorType)
            if ok then
                descriptor <- v
                true
            else
                false

type DeviceCapabilityRegistry() =
    let effects = Dictionary<string, IActorEffect>(StringComparer.Ordinal)
    let guards = Dictionary<string, IActorGuard>(StringComparer.Ordinal)

    do
        effects.["ActuateMotor"] <- { new IActorEffect with
            member _.ExecuteAsync(_: ActorCapabilityContext, _: CancellationToken) : ValueTask = ValueTask.CompletedTask }
        guards.["CheckBattery"] <- { new IActorGuard with
            member _.EvaluateAsync(_: ActorCapabilityContext, _: CancellationToken) : ValueTask<bool> = ValueTask.FromResult(true) }

    interface ICapabilityRegistry with
        member _.TryGetEffect(name: string, [<Out>] effect: byref<IActorEffect>) : bool =
            let ok, v = effects.TryGetValue(name)
            if ok then effect <- v; true else false
        member _.TryGetGuard(name: string, [<Out>] guard: byref<IActorGuard>) : bool =
            let ok, v = guards.TryGetValue(name)
            if ok then guard <- v; true else false
