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

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.Regression;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Real-sidecar compatibility coverage for the host turn lock, beyond cancellation isolation.
/// Uses real placement, scheduler and actor state storage; DAPR_RUNTIME_VERSION selects the release.
/// </summary>
public sealed class ActorTurnCompatibilityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ReentrantChainUnwindsBeforeUnrelatedTurn(bool useRemoting, bool perTypeReentrancy)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var probe = new TurnCompatibilityProbe();
        await using var app = await CreateTestAppAsync(probe, true, perTypeReentrancy, timeout.Token);
        using var scope = app.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var firstId = ActorId.CreateRandom();
        var secondId = ActorId.CreateRandom();
        var typed = factory.CreateActorProxy<ITurnCompatibilityActor>(firstId, nameof(TurnCompatibilityActor));
        var weak = factory.Create(firstId, nameof(TurnCompatibilityActor));
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(typed, timeout.Token);
        var request = new ReentrantTurnRequest
        {
            ActorIds = [firstId.ToString(), secondId.ToString(), firstId.ToString(), secondId.ToString(), firstId.ToString()],
            UseRemoting = useRemoting
        };

        var chain = useRemoting
            ? typed.Reenter(request, timeout.Token)
            : weak.InvokeMethodAsync(nameof(ITurnCompatibilityActor.Reenter), request, timeout.Token);
        Task<int>? unrelated = null;
        try
        {
            await probe.Held.Task.WaitAsync(timeout.Token);
            Assert.Equal(["enter:0", "enter:1", "enter:2", "enter:3", "enter:4"], probe.Events.ToArray());
            unrelated = useRemoting
                ? typed.Increment(timeout.Token)
                : weak.InvokeMethodAsync<int>(nameof(ITurnCompatibilityActor.Increment), timeout.Token);

            await Task.WhenAny(unrelated, Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token));
            Assert.False(unrelated.IsCompleted, "An unrelated call entered a still-running reentrant chain.");
        }
        finally
        {
            probe.Release.TrySetResult();
            await chain.WaitAsync(timeout.Token);
            if (unrelated is not null)
            {
                await unrelated.WaitAsync(timeout.Token);
            }
        }

        Assert.Equal(1, await unrelated!);
        Assert.Equal(
            ["enter:0", "enter:1", "enter:2", "enter:3", "enter:4",
                "exit:4", "exit:3", "exit:2", "exit:1", "exit:0", $"increment:{firstId}"],
            probe.Events.ToArray());
        Assert.Equal(2, await typed.Increment(timeout.Token));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task StateCachingRollbackAndConcurrentUpdatesRemainCorrect(bool useRemoting, bool reentrancy)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var app = await CreateTestAppAsync(new TurnCompatibilityProbe(), reentrancy, false, timeout.Token);
        using var scope = app.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var id = ActorId.CreateRandom();
        var typed = factory.CreateActorProxy<ITurnCompatibilityActor>(id, nameof(TurnCompatibilityActor));
        var weak = factory.Create(id, nameof(TurnCompatibilityActor));
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(typed, timeout.Token);

        Task<TurnStateSnapshot> Read(bool reload = false) => useRemoting
            ? typed.Read(reload, timeout.Token)
            : weak.InvokeMethodAsync<bool, TurnStateSnapshot>(nameof(ITurnCompatibilityActor.Read), reload, timeout.Token);
        Task<int> Increment() => useRemoting
            ? typed.Increment(timeout.Token)
            : weak.InvokeMethodAsync<int>(nameof(ITurnCompatibilityActor.Increment), timeout.Token);

        Assert.Equal(1, await Increment());
        var first = await Read();
        var cached = await Read();
        Assert.Equal(first.InstanceId, cached.InstanceId);
        Assert.Equal(1, cached.Value);
        Assert.True(first.SameWithinTurn);
        Assert.True(cached.SameWithinTurn);
        // Reentrant requests intentionally have separate contextual state caches.
        Assert.Equal(!reentrancy, cached.SameAsPreviousRead);

        if (useRemoting)
        {
            await Assert.ThrowsAsync<ActorMethodInvocationException>(() => typed.FailAfterChangingState(timeout.Token));
        }
        else
        {
            await Assert.ThrowsAsync<DaprApiException>(() =>
                weak.InvokeMethodAsync(nameof(ITurnCompatibilityActor.FailAfterChangingState), timeout.Token));
        }

        var afterFailure = await Read();
        Assert.Equal(first.InstanceId, afterFailure.InstanceId);
        Assert.Equal(1, afterFailure.Value);
        Assert.False(afterFailure.SameAsPreviousRead);
        Assert.False(afterFailure.TransientExists);
        Assert.True(afterFailure.SentinelExists);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Increment())).WaitAsync(timeout.Token);
        Assert.Equal(Enumerable.Range(2, 12), results.Order());

        var final = await Read();
        var persisted = await Read(reload: true);
        Assert.Equal(first.InstanceId, final.InstanceId);
        Assert.Equal(first.InstanceId, persisted.InstanceId);
        Assert.Equal(13, final.Value);
        Assert.Equal(13, persisted.Value);
        Assert.False(persisted.SameAsPreviousRead);
        Assert.True(persisted.SameWithinTurn);
        Assert.False(persisted.TransientExists);
        Assert.True(persisted.SentinelExists);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ScheduledCallbacksSerializeWithMethodsWithoutBlockingOtherActors(bool reminder, bool reentrancy)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var probe = new TurnCompatibilityProbe();
        await using var app = await CreateTestAppAsync(probe, reentrancy, false, timeout.Token);
        using var scope = app.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var id = ActorId.CreateRandom();
        var typed = factory.CreateActorProxy<ITurnCompatibilityActor>(id, nameof(TurnCompatibilityActor));
        var otherId = ActorId.CreateRandom();
        var other = factory.CreateActorProxy<ITurnCompatibilityActor>(otherId, nameof(TurnCompatibilityActor));
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(typed, timeout.Token);
        var initial = await typed.Read(false, timeout.Token);
        var payload = Guid.NewGuid().ToByteArray();
        Task<int>? queued = null;
        try
        {
            await typed.StartSchedule(new TurnScheduleRequest
            {
                Reminder = reminder, CallbackActorId = otherId.ToString(), Payload = payload
            }, timeout.Token);
            await probe.Held.Task.WaitAsync(timeout.Token);
            queued = typed.Increment(timeout.Token);

            // A held callback on one ID must not lock the entire actor type.
            Assert.Equal(1, await other.Increment(timeout.Token).WaitAsync(TimeSpan.FromSeconds(10), timeout.Token));
            await Task.WhenAny(queued, Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token));
            Assert.False(queued.IsCompleted, "An ordinary method overlapped a scheduled callback.");
            Assert.Equal(["callback-enter:1", $"increment:{otherId}"], probe.Events.ToArray());
            probe.Release.TrySetResult();

            Assert.Equal(1, await queued.WaitAsync(timeout.Token));
            await probe.ScheduleFinished.Task.WaitAsync(timeout.Token);
            var completed = await typed.Read(true, timeout.Token);
            Assert.Equal(initial.InstanceId, completed.InstanceId);
            Assert.Equal(1, completed.Value);
            Assert.Equal(2, completed.CallbackCount);
            Assert.Equal(2, probe.CallsFromCallbacks);
            Assert.Equal(2, probe.Payloads.Count);
            Assert.All(probe.Payloads, actual => Assert.Equal(payload, actual));

            var events = probe.Events.ToArray();
            var methodIndex = Array.IndexOf(events, $"increment:{id}");
            Assert.True(methodIndex > Array.IndexOf(events, "callback-exit:1"));
            Assert.True(Array.IndexOf(events, "callback-enter:2") > Array.IndexOf(events, "callback-exit:1"));
            Assert.False(methodIndex > Array.IndexOf(events, "callback-enter:2") &&
                         methodIndex < Array.IndexOf(events, "callback-exit:2"));

            // Observe several periods after unregistering, not just the instant it completes.
            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            Assert.Equal(2, (await typed.Read(true, timeout.Token)).CallbackCount);
            Assert.Equal(2, probe.Payloads.Count);
        }
        finally
        {
            probe.Release.TrySetResult();
            if (queued is not null)
            {
                await queued.WaitAsync(timeout.Token);
            }
        }
    }

    private async Task<ActorTestContext> CreateTestAppAsync(TurnCompatibilityProbe probe,
        bool reentrancy, bool perTypeReentrancy, CancellationToken cancellationToken)
    {
        var options = new DaprRuntimeOptions("1.18.4");
        output.WriteLine($"Dapr runtime: {options.Version}; reentrancy: {reentrancy}; per-type: {perTypeReentrancy}");
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            options: options, needsActorState: true, cancellationToken: cancellationToken);
        try
        {
            await environment.StartAsync(cancellationToken);
            var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-turn-compatibility-components");
            var harness = new DaprHarnessBuilder(componentsDir).WithOptions(options).WithEnvironment(environment).BuildActors();
            var app = await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddSingleton(probe);
                    builder.Services.AddActors(actorOptions =>
                    {
                        actorOptions.ReentrancyConfig = new ActorReentrancyConfig
                        {
                            Enabled = reentrancy && !perTypeReentrancy
                        };
                        if (perTypeReentrancy)
                        {
                            actorOptions.Actors.RegisterActor<TurnCompatibilityActor>(new ActorRuntimeOptions
                            {
                                ReentrancyConfig = new ActorReentrancyConfig { Enabled = reentrancy }
                            });
                        }
                        else
                        {
                            actorOptions.Actors.RegisterActor<TurnCompatibilityActor>();
                        }
                    });
                })
                .ConfigureApp(app => app.MapActorsHandlers())
                .BuildAndStartAsync();
            return new ActorTestContext(environment, app);
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }
}
