// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Interpreted;
using System.Text.Json;

namespace Dapr.Actors.Next.Examples.DeviceManagement;

public static class DeviceManagementDemo
{
    public const string SmartLockDeviceType = "SmartLock";

    public static InterpretedMachineDefinition SmartLockDefinition(bool includeUnlockingCompletion = true, string motorEffect = "ActuateMotor") =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Locked",
            States =
            [
                new InterpretedStateDefinition { Name = "Locked" },
                new InterpretedStateDefinition { Name = "Unlocking" },
                new InterpretedStateDefinition { Name = "Unlocked" },
                new InterpretedStateDefinition { Name = "Locking" },
            ],
            Transitions = Transitions(includeUnlockingCompletion, motorEffect),
        };

    public static InterpretedMachineVerificationResult Verify(
        IInterpretedMachineVerifier verifier,
        InterpretedMachineDefinition definition) =>
        verifier.Verify(definition);

    private static IReadOnlyList<InterpretedTransitionDefinition> Transitions(bool includeUnlockingCompletion, string motorEffect)
    {
        var transitions = new List<InterpretedTransitionDefinition>
        {
            new()
            {
                Source = "Locked",
                Event = "Unlock",
                Branches =
                [
                    new InterpretedBranchDefinition
                    {
                        Guards = ["CheckBattery"],
                        Target = "Unlocking",
                        Effects = [motorEffect],
                    },
                ],
            },
            new()
            {
                Source = "Unlocked",
                Event = "Lock",
                Branches =
                [
                    new InterpretedBranchDefinition
                    {
                        Otherwise = true,
                        Target = "Locking",
                        Effects = [motorEffect],
                    },
                ],
            },
            new()
            {
                Source = "Locking",
                Event = "MotorStopped",
                Branches =
                [
                    new InterpretedBranchDefinition
                    {
                        Otherwise = true,
                        Target = "Locked",
                    },
                ],
            },
        };

        if (includeUnlockingCompletion)
        {
            transitions.Add(new InterpretedTransitionDefinition
            {
                Source = "Unlocking",
                Event = "MotorStopped",
                Branches =
                [
                    new InterpretedBranchDefinition
                    {
                        Otherwise = true,
                        Target = "Unlocked",
                    },
                ],
            });
        }

        return transitions;
    }
}

public sealed class DeviceControlPlane(IActorRegistry registry, IDynamicActorClient client)
{
    public IReadOnlyList<string> ListDeviceOperations() =>
        registry.Actors
            .SelectMany(actor => actor.Methods.Select(method => $"{actor.ActorType}.{method.Name}({string.Join(", ", method.Parameters.Select(parameter => parameter.ParameterType.Name))})"))
            .Order(StringComparer.Ordinal)
            .ToArray();

    public Task<string?> SendCommandAsync(string deviceType, string deviceId, string command, CancellationToken cancellationToken = default) =>
        RaiseAsync(deviceType, deviceId, command, new DeviceCommand(command), cancellationToken);

    public Task<string?> RecordSensorEventAsync(string deviceType, string deviceId, string sensorEvent, CancellationToken cancellationToken = default) =>
        RaiseAsync(deviceType, deviceId, sensorEvent, new DeviceSensorEvent(sensorEvent), cancellationToken);

    private Task<string?> RaiseAsync<TPayload>(
        string deviceType,
        string deviceId,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var evt = new InterpretedEvent(eventName, JsonSerializer.SerializeToElement(payload));
        return client.InvokeAsync(deviceType, deviceId, "Raise", JsonSerializer.Serialize(evt), cancellationToken);
    }
}

public sealed class DeviceTypeRegistry : IActorRegistry
{
    private readonly Dictionary<string, ActorTypeDescriptor> devices = new(StringComparer.Ordinal);

    public IReadOnlyList<ActorTypeDescriptor> Actors => devices.Values.OrderBy(device => device.ActorType, StringComparer.Ordinal).ToArray();

    public void AddDeviceType(string deviceType)
    {
        devices[deviceType] = new ActorTypeDescriptor(
            deviceType,
            1,
            typeof(InterpretedStateMachineActor),
            typeof(InterpretedStateMachineActor),
            [
                new ActorMethodDescriptor(
                    "Raise",
                    "Raise",
                    typeof(InterpretedRaiseResult),
                    [new ActorParameterDescriptor("evt", typeof(InterpretedEvent), 0, false, false, null)]),
            ]);
    }

    public bool TryGet(string actorType, out ActorTypeDescriptor descriptor) => devices.TryGetValue(actorType, out descriptor!);
}

public sealed class DeviceCapabilityRegistry : ICapabilityRegistry
{
    private readonly Dictionary<string, IActorEffect> effects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IActorGuard> guards = new(StringComparer.Ordinal);

    public DeviceCapabilityRegistry()
    {
        effects["ActuateMotor"] = new MotorEffect();
        guards["CheckBattery"] = new BatteryGuard();
    }

    public bool TryGetEffect(string name, out IActorEffect effect) => effects.TryGetValue(name, out effect!);

    public bool TryGetGuard(string name, out IActorGuard guard) => guards.TryGetValue(name, out guard!);

    private sealed class MotorEffect : IActorEffect
    {
        public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class BatteryGuard : IActorGuard
    {
        public ValueTask<bool> EvaluateAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }
}

public sealed record DeviceCommand(string Name);

public sealed record DeviceSensorEvent(string Name);
