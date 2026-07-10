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

using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Examples.DeviceManagement;
using Dapr.Actors.Next.Interpreted;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Examples.DeviceManagement.Tests;

public sealed class DeviceManagementTests
{
    private static readonly ActorId FrontDoor = ActorId.Create("front-door");

    [Fact]
    public async Task Control_plane_lists_device_type_and_sends_command_without_contract()
    {
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, DeviceManagementDemo.SmartLockDefinition());
        await using var runtime = CreateRuntime(store);
        var provider = Provider(runtime);
        var controller = new DeviceControlPlane(
            provider.GetRequiredService<IActorRegistry>(),
            provider.GetRequiredService<IDynamicActorClient>());

        Assert.Contains("SmartLock.Raise(InterpretedEvent)", controller.ListDeviceOperations());

        var unlock = controller.SendCommandAsync("SmartLock", FrontDoor.Value, "Unlock");
        await runtime.RunToIdle();
        var unlocked = await unlock;

        Assert.Equal("Unlocking", State(unlocked));

        var stopped = controller.RecordSensorEventAsync("SmartLock", FrontDoor.Value, "MotorStopped");
        await runtime.RunToIdle();

        Assert.Equal("Unlocked", State(await stopped));
    }

    [Fact]
    public void Verification_rejects_device_type_that_can_strand_a_lock()
    {
        var verifier = new InterpretedMachineVerifier(new DeviceCapabilityRegistry());
        var stranded = DeviceManagementDemo.SmartLockDefinition(includeUnlockingCompletion: false);

        var result = DeviceManagementDemo.Verify(verifier, stranded);

        Assert.False(result.IsValid);
        Assert.Contains(result.Defects, defect => defect.Contains("State 'Unlocking' is a dead end", StringComparison.Ordinal));
    }

    [Fact]
    public void Well_formed_device_type_definition_passes_verification()
    {
        var verifier = new InterpretedMachineVerifier(new DeviceCapabilityRegistry());

        var result = DeviceManagementDemo.Verify(verifier, DeviceManagementDemo.SmartLockDefinition());

        Assert.True(result.IsValid);
        result.ThrowIfInvalid();
    }

    [Fact]
    public async Task Definition_with_unregistered_effect_is_rejected_before_rollout()
    {
        var registry = new DeviceCapabilityRegistry();
        var verifier = new InterpretedMachineVerifier(registry);
        var store = new InMemoryInterpretedMachineStore();
        var deployer = new InterpretedMachineDeployer(verifier, store);
        var definition = DeviceManagementDemo.SmartLockDefinition(motorEffect: "UnknownMotor");

        Assert.False(registry.TryGetEffect("UnknownMotor", out _));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => deployer.DeployAsync("SmartLock", FrontDoor, definition).AsTask());
        Assert.Contains("Effect 'UnknownMotor'", ex.Message, StringComparison.Ordinal);
        Assert.Null(await store.GetAsync("SmartLock", FrontDoor));
    }

    private static ActorTestRuntime CreateRuntime(IInterpretedMachineStore store)
    {
        return new ActorTestRuntime(services =>
        {
            var deviceTypes = new DeviceTypeRegistry();
            deviceTypes.AddDeviceType(DeviceManagementDemo.SmartLockDeviceType);
            services.AddSingleton<IActorRegistry>(deviceTypes);
            services.AddSingleton(store);
            services.AddSingleton<ICapabilityRegistry, DeviceCapabilityRegistry>();
            services.AddDaprInterpretedActors(DeviceManagementDemo.SmartLockDeviceType);
        });
    }

    private static IServiceProvider Provider(ActorTestRuntime runtime) =>
        (IServiceProvider)runtime.GetType().GetField("provider", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;

    private static async Task Deploy(InMemoryInterpretedMachineStore store, InterpretedMachineDefinition definition)
    {
        var deployer = new InterpretedMachineDeployer(
            new InterpretedMachineVerifier(new DeviceCapabilityRegistry()),
            store);
        await deployer.DeployAsync("SmartLock", FrontDoor, definition);
    }

    private static string? State(string? result)
    {
        using var document = JsonDocument.Parse(result!);
        return document.RootElement.GetProperty("State").GetString();
    }
}
