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

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Dapr.Actors.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dapr.Actors.Runtime;

public sealed class ActorManagerActivationIdTests
{
    private readonly StateStore store = new();
    private readonly ActorManager manager;
    private readonly ActorId id = ActorId.CreateRandom();

    public ActorManagerActivationIdTests()
    {
        var registration = new ActorRegistration(ActorTypeInformation.Get(typeof(CountingActor), actorTypeName: null));
        manager = new ActorManager(registration, new DefaultActorActivator(), JsonSerializerDefaults.Web, false, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory, store);
    }

    [Fact]
    public async Task Method_SameActivationId_ReusesActivationAndCachedState()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();

        Assert.Equal(7, await IncrementAsync("a1"));

        Assert.Same(first, GetActor());
        Assert.False(first.IsDeactivated);
        VerifyStoreReads(1);
    }

    [Fact]
    public async Task Method_NewActivationId_DeactivatesOldAndActivatesFresh()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();

        Assert.Equal(6, await IncrementAsync("a2"));
        var second = GetActor();

        Assert.NotSame(first, second);
        Assert.True(first.IsDeactivated);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsActivated);
        Assert.False(second.IsDeactivated);
        VerifyStoreReads(2);

        Assert.Equal(7, await IncrementAsync("a2"));
        Assert.Same(second, GetActor());
        VerifyStoreReads(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Method_MissingActivationId_NeverResets(string missing)
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();

        Assert.Equal(7, await IncrementAsync(missing));
        Assert.Equal(8, await IncrementAsync(missing));

        Assert.Same(first, GetActor());
        Assert.False(first.IsDeactivated);
        VerifyStoreReads(1);

        Assert.Equal(9, await IncrementAsync("a1"));
        Assert.Same(first, GetActor());
    }

    [Fact]
    public async Task Method_NoActivationIdEver_KeepsTodaysBehaviour()
    {
        Assert.Equal(6, await IncrementAsync(null));
        var first = GetActor();

        Assert.Equal(7, await IncrementAsync(null));

        Assert.Same(first, GetActor());
        VerifyStoreReads(1);
    }

    [Fact]
    public async Task Method_StoredEmptyThenNonEmpty_Resets()
    {
        Assert.Equal(6, await IncrementAsync(null));
        var first = GetActor();

        Assert.Equal(6, await IncrementAsync("a1"));

        Assert.NotSame(first, GetActor());
        Assert.True(first.IsDeactivated);
        VerifyStoreReads(2);
    }

    [Fact]
    public async Task Method_ActivationIdIsCaseSensitive()
    {
        await IncrementAsync("abc");
        var first = GetActor();

        await IncrementAsync("ABC");

        Assert.NotSame(first, GetActor());
        Assert.True(first.IsDeactivated);
    }

    [Fact]
    public async Task ActivateActorAsync_StoresActivationId()
    {
        await manager.ActivateActorAsync(id, "a1");
        var first = GetActor();

        await IncrementAsync("a1");
        Assert.Same(first, GetActor());

        await IncrementAsync("a2");
        Assert.NotSame(first, GetActor());
    }

    [Fact]
    public async Task Deactivate_ThenSameActivationId_ActivatesFresh()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await manager.DeactivateActorAsync(id);
        Assert.True(first.IsDeactivated);
        Assert.False(manager.TryGetActorAsync(id, out _));

        Assert.Equal(6, await IncrementAsync("a1"));
        Assert.NotSame(first, GetActor());
        VerifyStoreReads(2);
    }

    [Fact]
    public async Task Reminder_SameActivationId_Reuses()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await FireReminderAsync("a1");

        Assert.Same(first, GetActor());
        Assert.Equal(1, first.Reminders);
        Assert.False(first.IsDeactivated);
    }

    [Fact]
    public async Task Reminder_NewActivationId_Resets()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await FireReminderAsync("a2");
        var second = GetActor();

        Assert.NotSame(first, second);
        Assert.True(first.IsDeactivated);
        Assert.Equal(0, first.Reminders);
        Assert.Equal(1, second.Reminders);
        Assert.Equal(6, await IncrementAsync("a2"));
        Assert.Same(second, GetActor());
    }

    [Fact]
    public async Task Reminder_MissingActivationId_NeverResets()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await FireReminderAsync(null);

        Assert.Same(first, GetActor());
        Assert.Equal(1, first.Reminders);
    }

    [Fact]
    public async Task Timer_SameActivationId_Reuses()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await FireTimerAsync("a1");

        Assert.Same(first, GetActor());
        Assert.Equal(1, first.Timers);
    }

    [Fact]
    public async Task Timer_NewActivationId_Resets()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await FireTimerAsync("a2");
        var second = GetActor();

        Assert.NotSame(first, second);
        Assert.True(first.IsDeactivated);
        Assert.Equal(0, first.Timers);
        Assert.Equal(1, second.Timers);
    }

    [Fact]
    public async Task Timer_MissingActivationId_NeverResets()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        await FireTimerAsync("");

        Assert.Same(first, GetActor());
        Assert.Equal(1, first.Timers);
    }

    [Fact]
    public async Task Timer_CallbackThrows_SkipsSave()
    {
        await IncrementAsync("a1");
        var saves = store.Saves.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => FireTimerAsync("a1", nameof(CountingActor.ThrowingTimerAsync)));

        Assert.Equal(saves, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Fact]
    public async Task Method_ReplacedWhileRunning_SkipsSaveAndFails()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();

        var held = HoldAsync("a1");
        await WaitAsync(first.Held);
        var replacing = IncrementAsync("a2");
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.False(first.IsDeactivated);
        first.Release.SetResult();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Contains("replaced", ex.Message);
        Assert.Equal(6, await replacing);
        Assert.True(first.IsDeactivated);
        Assert.True(first.IsDisposed);
        var second = GetActor();
        Assert.NotSame(first, second);
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
        Assert.Equal(7, await IncrementAsync("a2"));
    }

    [Fact]
    public async Task Method_ReplacingCallCancelledWhileWaiting_StaleDeactivatedAfterTurn()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = HoldAsync("a1");
        await WaitAsync(first.Held);
        using var cts = new CancellationTokenSource();
        var replacing = manager.DispatchWithoutRemotingAsync(id, nameof(ICountingActor.IncrementAsync), new MemoryStream(), new MemoryStream(), "a2", cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replacing);
        Assert.False(first.IsDeactivated);

        first.Release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        await WaitAsync(first.Deactivated);
        await IncrementAsync("a2");
        Assert.NotSame(first, GetActor());
    }

    [Fact]
    public async Task Method_DeactivatedWhileRunning_SkipsSaveAndFails()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = HoldAsync("a1");
        await WaitAsync(first.Held);
        var deactivating = manager.DeactivateActorAsync(id);
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.False(first.IsDeactivated);
        first.Release.SetResult();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Contains("deactivated", ex.Message);
        await deactivating;
        Assert.True(first.IsDeactivated);
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.Single(store.Saves);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Fact]
    public async Task Reminder_ReplacedWhileRunning_SkipsSaveAndFails()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = FireReminderAsync("a1", "hold");
        await WaitAsync(first.Held);
        var replacing = IncrementAsync("a2");
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.False(first.IsDeactivated);
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Equal(6, await replacing);
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Fact]
    public async Task Timer_ReplacedWhileRunning_SkipsSaveAndFails()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = FireTimerAsync("a1", nameof(CountingActor.HoldTimerAsync));
        await WaitAsync(first.Held);
        var replacing = IncrementAsync("a2");
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.False(first.IsDeactivated);
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Equal(6, await replacing);
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Theory]
    [InlineData("a1")]
    [InlineData(null)]
    public async Task Method_NotReplacedWhileRunning_StillSaves(string otherActivationId)
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = HoldAsync("a1");
        await WaitAsync(first.Held);
        var other = IncrementAsync(otherActivationId);
        Assert.False(other.IsCompleted);
        first.Release.SetResult();

        Assert.Equal(100, await held);
        Assert.Equal(101, await other);
        Assert.Same(first, GetActor());
        Assert.False(first.IsDeactivated);
        Assert.Equal(3, store.Saves.Count);
        Assert.Contains("100", store.Saves.ElementAt(1));
        Assert.Contains("101", store.Saves.Last());
    }

    [Fact]
    public async Task Method_ReplacedDuringPostMethodHook_SkipsSaveAndFails()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();
        first.HoldInPostHook = true;

        var held = IncrementAsync("a1");
        await WaitAsync(first.PostHeld);
        var replacing = IncrementAsync("a2");
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.False(first.IsDeactivated);
        first.PostRelease.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Equal(6, await replacing);
        Assert.NotSame(first, GetActor());
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Fact]
    public async Task ReentrantCallInSameChain_DifferentActivationId_DoesNotReset()
    {
        var registration = new ActorRegistration(ActorTypeInformation.Get(typeof(CountingActor), actorTypeName: null));
        var reentrant = new ActorManager(registration, new DefaultActorActivator(), JsonSerializerDefaults.Web, false, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory, store, new ActorReentrancyConfig { Enabled = true });
        ActorReentrancyContextAccessor.ReentrancyContext = "chain";
        try
        {
            Assert.Equal(6, await DispatchAsync(reentrant, nameof(ICountingActor.IncrementAsync), "a1"));
            Assert.True(reentrant.TryGetActorAsync(id, out var actor));
            var first = Assert.IsType<CountingActor>(actor);

            var held = DispatchAsync(reentrant, nameof(ICountingActor.HoldAsync), "a1");
            await WaitAsync(first.Held);
            var nested = DispatchAsync(reentrant, nameof(ICountingActor.IncrementAsync), "a2");

            await nested.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            first.Release.SetResult();
            Assert.Equal(100, await held);
            Assert.False(first.IsDeactivated);
            Assert.True(reentrant.TryGetActorAsync(id, out var registered));
            Assert.Same(first, registered);
        }
        finally
        {
            ActorReentrancyContextAccessor.ReentrancyContext = null;
        }
    }

    [Fact]
    public async Task SupersededWaiter_DoesNotEvictNewerActivation()
    {
        await IncrementAsync("a0");
        var first = GetActor();
        var held = HoldAsync("a0");
        await WaitAsync(first.Held);

        var context = new ManualSynchronizationContext();
        var older = context.Run(() => IncrementAsync("a1"));
        first.Release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => held);

        Assert.Equal(6, await IncrementAsync("a2"));
        var newer = GetActor();
        await context.DrainAsync(older);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => older);
        Assert.Contains("newer activation", ex.Message);
        Assert.Same(newer, GetActor());
        Assert.False(newer.IsDeactivated);
        Assert.Equal(7, await IncrementAsync("a2"));
    }

    [Fact]
    public async Task Method_DeactivatedWhileRunning_ExplicitSaveIsRefused_DeactivateStillSaves()
    {
        await IncrementAsync("a1");
        var first = GetActor();
        first.SaveInHold = true;

        var held = HoldAsync("a1");
        await WaitAsync(first.Held);
        var deactivating = manager.DeactivateActorAsync(id);
        first.SaveOnDeactivate = true;
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.True(first.SaveRefused);
        await deactivating;
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
        Assert.Contains(store.Saves, s => s.Contains("200"));
    }

    [Fact]
    public async Task Deactivate_LosesTurnToNewCall_OldIsDeactivatedBeforeNewActivates()
    {
        await IncrementAsync("a1");
        var first = GetActor();
        var held = HoldAsync("a1");
        await WaitAsync(first.Held);

        var context = new ManualSynchronizationContext();
        var deactivating = context.Run(() => manager.DeactivateActorAsync(id));
        first.Release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => held);

        Assert.Equal(6, await IncrementAsync("a1"));
        var second = GetActor();
        Assert.True(first.IsDeactivated);
        Assert.True(first.DeactivatedBefore(second));

        await context.DrainAsync(deactivating);
        await deactivating;
        Assert.Same(second, GetActor());
        Assert.False(second.IsDeactivated);
    }

    [Fact]
    public async Task Replacement_LosesTurnToNewCall_StaleIsDeactivatedBeforeNewActivates()
    {
        await IncrementAsync("a1");
        var first = GetActor();
        var held = HoldAsync("a1");
        await WaitAsync(first.Held);

        var context = new ManualSynchronizationContext();
        var replacing = context.Run(() => IncrementAsync("a2"));
        first.Release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => held);

        Assert.Equal(6, await IncrementAsync("a2"));
        var second = GetActor();
        Assert.True(first.IsDeactivated);
        Assert.True(first.DeactivatedBefore(second));

        await context.DrainAsync(replacing);
        Assert.Equal(7, await replacing);
        Assert.Same(second, GetActor());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReentrantCall_AfterActorDetached_IsRejected(bool delete)
    {
        var registration = new ActorRegistration(ActorTypeInformation.Get(typeof(CountingActor), actorTypeName: null));
        var reentrant = new ActorManager(registration, new DefaultActorActivator(), JsonSerializerDefaults.Web, false, NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory, store, new ActorReentrancyConfig { Enabled = true });
        ActorReentrancyContextAccessor.ReentrancyContext = "chain";
        try
        {
            await DispatchAsync(reentrant, nameof(ICountingActor.IncrementAsync), "a1");
            Assert.True(reentrant.TryGetActorAsync(id, out var actor));
            var first = Assert.IsType<CountingActor>(actor);
            var held = DispatchAsync(reentrant, nameof(ICountingActor.HoldAsync), "a1");
            await WaitAsync(first.Held);

            ActorReentrancyContextAccessor.ReentrancyContext = "other";
            var detaching = delete ? reentrant.DeactivateActorAsync(id) : DispatchAsync(reentrant, nameof(ICountingActor.IncrementAsync), "a2");
            ActorReentrancyContextAccessor.ReentrancyContext = "chain";
            Assert.False(reentrant.TryGetActorAsync(id, out _));

            var nested = DispatchAsync(reentrant, nameof(ICountingActor.IncrementAsync), "a1");
            await Assert.ThrowsAsync<InvalidOperationException>(() => nested.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.False(reentrant.TryGetActorAsync(id, out _));
            Assert.False(first.IsDeactivated);

            first.Release.SetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => held);
            await detaching;
            Assert.True(first.IsDeactivated);
        }
        finally
        {
            ActorReentrancyContextAccessor.ReentrancyContext = null;
        }
    }

    [Fact]
    public async Task Reset_OnDeactivateThrows_NewCallStillSucceeds()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();
        first.ThrowOnDeactivate = true;

        Assert.Equal(6, await IncrementAsync("a2"));

        var second = GetActor();
        Assert.NotSame(first, second);
        Assert.True(first.IsDeactivated);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsActivated);
        Assert.Equal(7, await IncrementAsync("a2"));
    }

    [Fact]
    public async Task Reset_OnDeactivateSave_IsRefused()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();
        first.SaveOnDeactivate = true;

        Assert.Equal(6, await IncrementAsync("a2"));
        Assert.True(first.SaveRefused);
        Assert.DoesNotContain(store.Saves, s => s.Contains("200"));

        var second = GetActor();
        second.SaveOnDeactivate = true;
        await manager.DeactivateActorAsync(id);
        Assert.False(second.SaveRefused);
        Assert.Contains(store.Saves, s => s.Contains("200"));
    }

    [Fact]
    public async Task Method_ReplacedWhileRunning_ExplicitSaveIsRefused()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();
        first.SaveInHold = true;

        var held = HoldAsync("a1");
        await WaitAsync(first.Held);
        var replacing = IncrementAsync("a2");
        Assert.False(manager.TryGetActorAsync(id, out _));
        Assert.False(first.IsDeactivated);
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.True(first.SaveRefused);
        Assert.Equal(6, await replacing);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Fact]
    public async Task Deactivate_OnDeactivateThrows_StillThrows()
    {
        await IncrementAsync("a1");
        var first = GetActor();
        first.ThrowOnDeactivate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DeactivateActorAsync(id));

        Assert.True(first.IsDisposed);
        Assert.False(manager.TryGetActorAsync(id, out _));
    }

    [Fact]
    public async Task ConcurrentFirstActivation_LateDifferentId_FailsAndNewerActivatesFresh()
    {
        var race = await StartActivationRaceAsync("a1", "a2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => race.Late);
        Assert.Equal(6, await race.Newer);

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.NotSame(race.Gate.Actor, registered);
        Assert.True(race.Gate.Actor.IsDeactivated);
        Assert.Equal(2, race.Store.Saves.Count);
        Assert.Equal(7, await DispatchAsync(race.Manager, nameof(ICountingActor.IncrementAsync), "a2"));
        Assert.Contains("\"value\":7}", race.Store.Saves.Last());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ConcurrentFirstActivation_LateMissingId_FailsAndNewerActivatesFresh(string lateId)
    {
        var race = await StartActivationRaceAsync(lateId, "a2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => race.Late);
        Assert.Equal(6, await race.Newer);

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.NotSame(race.Gate.Actor, registered);
        Assert.Equal(2, race.Store.Saves.Count);
    }

    [Fact]
    public async Task ConcurrentFirstActivation_LateSameId_NewerReusesIt()
    {
        var race = await StartActivationRaceAsync("a2", "a2");

        Assert.Equal(6, await race.Late);
        Assert.Equal(7, await race.Newer);

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.Same(race.Gate.Actor, registered);
        Assert.False(race.Gate.Actor.IsDeactivated);
        Assert.Contains("\"value\":7}", race.Store.Saves.Last());
    }

    [Theory]
    [InlineData("a1")]
    [InlineData(null)]
    public async Task ConcurrentFirstActivation_DiscardedOnDeactivateThrows_IsLogged(string lateId)
    {
        var logs = new CapturingLoggerFactory();
        var race = await StartActivationRaceAsync(lateId, "a2", discardedThrowsOnDeactivate: true, logs);

        await Assert.ThrowsAsync<InvalidOperationException>(() => race.Late);
        Assert.Equal(6, await race.Newer);

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.NotSame(race.Gate.Actor, registered);

        var error = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.IsType<InvalidTimeZoneException>(error.Exception);
        Assert.StartsWith("Deactivating discarded activation", error.Message);
    }

    private async Task<(ActorManager Manager, StateStore Store, Task<int> Late, Task<int> Newer, ActivationGate Gate)> StartActivationRaceAsync(string lateId, string newerId, bool discardedThrowsOnDeactivate = false, ILoggerFactory loggerFactory = null)
    {
        var raceStore = new StateStore();
        var registration = new ActorRegistration(ActorTypeInformation.Get(typeof(GatedActivateActor), actorTypeName: null));
        var raceManager = new ActorManager(registration, new DefaultActorActivator(), JsonSerializerDefaults.Web, false, loggerFactory ?? NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory, raceStore);
        var gate = new ActivationGate { ThrowOnDeactivate = discardedThrowsOnDeactivate };
        GatedActivateActor.Gate = gate;

        var late = DispatchAsync(raceManager, nameof(ICountingActor.IncrementAsync), lateId);
        await WaitAsync(gate.Started);

        var newer = DispatchAsync(raceManager, nameof(ICountingActor.IncrementAsync), newerId);
        Assert.False(newer.IsCompleted);

        gate.Release.SetResult();
        return (raceManager, raceStore, late, newer, gate);
    }

    private static Task WaitAsync(TaskCompletionSource signal) =>
        signal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private Task<int> HoldAsync(string activationId) => DispatchAsync(nameof(ICountingActor.HoldAsync), activationId);

    private Task<int> IncrementAsync(string activationId) => DispatchAsync(nameof(ICountingActor.IncrementAsync), activationId);

    private Task<int> DispatchAsync(string method, string activationId) => DispatchAsync(manager, method, activationId);

    private async Task<int> DispatchAsync(ActorManager target, string method, string activationId)
    {
        using var response = new MemoryStream();
        await target.DispatchWithoutRemotingAsync(id, method, new MemoryStream(), response, activationId, TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize<int>(response.ToArray());
    }

    private Task FireReminderAsync(string activationId, string reminderName = "r1")
    {
        var body = new MemoryStream(Encoding.UTF8.GetBytes("{\"dueTime\":\"0h0m1s0ms\"}"));
        return manager.FireReminderAsync(id, reminderName, body, activationId, TestContext.Current.CancellationToken);
    }

    private Task FireTimerAsync(string activationId, string callback = nameof(CountingActor.OnTimerAsync))
    {
        var body = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"callback\":\"{callback}\"}}"));
        return manager.FireTimerAsync(id, body, activationId, TestContext.Current.CancellationToken);
    }

    private CountingActor GetActor()
    {
        Assert.True(manager.TryGetActorAsync(id, out var actor));
        return Assert.IsType<CountingActor>(actor);
    }

    private void VerifyStoreReads(int times) => Assert.Equal(times, store.Reads);

    private interface ICountingActor : IActor
    {
        Task<int> IncrementAsync();

        Task<int> HoldAsync();
    }

    private sealed class CountingActor : Actor, ICountingActor, IRemindable, IDisposable
    {
        public CountingActor(ActorHost host) : base(host)
        {
        }

        public bool IsActivated { get; private set; }

        public bool IsDeactivated { get; private set; }

        public bool IsDisposed { get; private set; }

        public int Reminders { get; private set; }

        public int Timers { get; private set; }

        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Deactivated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HoldInPostHook { get; set; }

        public TaskCompletionSource PostHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PostRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowOnDeactivate { get; set; }

        public bool SaveOnDeactivate { get; set; }

        public bool SaveInHold { get; set; }

        public bool SaveRefused { get; private set; }

        public async Task<int> IncrementAsync()
        {
            var current = await StateManager.TryGetStateAsync<int>("count");
            var next = (current.HasValue ? current.Value : 0) + 1;
            await StateManager.SetStateAsync("count", next);
            return next;
        }

        public async Task<int> HoldAsync()
        {
            Held.SetResult();
            await Release.Task;
            await StateManager.SetStateAsync("count", 100);
            if (SaveInHold)
            {
                await SaveRefusingAsync();
            }

            return 100;
        }

        private async Task SaveRefusingAsync()
        {
            try
            {
                await StateManager.SaveStateAsync();
            }
            catch (InvalidOperationException)
            {
                SaveRefused = true;
                throw;
            }
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            Reminders++;
            return reminderName == "hold" ? HoldAsync() : Task.CompletedTask;
        }

        public async Task HoldTimerAsync() => await HoldAsync();

        public async Task ThrowingTimerAsync()
        {
            await StateManager.SetStateAsync("count", 100);
            throw new InvalidOperationException("timer failed");
        }

        public Task OnTimerAsync()
        {
            Timers++;
            return Task.CompletedTask;
        }

        private static long sequence;

        private long activatedAt;

        private long deactivatedAt;

        public bool DeactivatedBefore(CountingActor other) => IsDeactivated && deactivatedAt < other.activatedAt;

        public void Dispose() => IsDisposed = true;

        protected override Task OnActivateAsync()
        {
            IsActivated = true;
            activatedAt = Interlocked.Increment(ref sequence);
            return Task.CompletedTask;
        }

        protected override async Task OnDeactivateAsync()
        {
            IsDeactivated = true;
            deactivatedAt = Interlocked.Increment(ref sequence);
            Deactivated.TrySetResult();
            if (ThrowOnDeactivate)
            {
                throw new InvalidOperationException("deactivate failed");
            }

            if (SaveOnDeactivate)
            {
                await StateManager.SetStateAsync("count", 200);
                await SaveRefusingAsync();
            }
        }

        protected override async Task OnPostActorMethodAsync(ActorMethodContext actorMethodContext)
        {
            if (HoldInPostHook)
            {
                HoldInPostHook = false;
                PostHeld.SetResult();
                await PostRelease.Task;
                await StateManager.SetStateAsync("count", 100);
            }
        }
    }

    private sealed class GatedActivateActor : Actor, ICountingActor
    {
        public GatedActivateActor(ActorHost host) : base(host)
        {
        }

        public static ActivationGate Gate { get; set; }

        private bool throwOnDeactivate;

        public bool IsDeactivated { get; private set; }

        public async Task<int> IncrementAsync()
        {
            var next = await StateManager.GetStateAsync<int>("count") + 1;
            await StateManager.SetStateAsync("count", next);
            return next;
        }

        public Task<int> HoldAsync() => throw new NotSupportedException();

        protected override async Task OnActivateAsync()
        {
            await StateManager.SetStateAsync("count", await StateManager.GetStateAsync<int>("count"));
            var gate = Gate;
            Gate = null;
            if (gate is not null)
            {
                gate.Actor = this;
                throwOnDeactivate = gate.ThrowOnDeactivate;
                gate.Started.SetResult();
                await gate.Release.Task;
            }
        }

        protected override Task OnDeactivateAsync()
        {
            IsDeactivated = true;
            return throwOnDeactivate ? throw new InvalidTimeZoneException("deactivate failed") : Task.CompletedTask;
        }
    }

    private sealed class ActivationGate
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowOnDeactivate { get; init; }

        public GatedActivateActor Actor { get; set; }
    }

    // Holds every continuation posted to it until the test drains it, so a waiter resumes only when the test says.
    private sealed class ManualSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> queue = new();

        public override void Post(SendOrPostCallback d, object state) => queue.Enqueue((d, state));

        public T Run<T>(Func<T> start)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                return start();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        public async Task DrainAsync(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                if (queue.TryDequeue(out var item))
                {
                    Run(() =>
                    {
                        item.Callback(item.State);
                        return 0;
                    });
                }
                else
                {
                    await Task.Delay(1, TestContext.Current.CancellationToken);
                }
            }
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory, ILogger
    {
        public ConcurrentQueue<(LogLevel Level, Exception Exception, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            Entries.Enqueue((logLevel, exception, formatter(state, exception)));

        public void Dispose()
        {
        }
    }

    private sealed class StateStore : IDaprInteractor
    {
        public int Reads { get; private set; }

        public ConcurrentQueue<string> Saves { get; } = new();

        public Task<ActorStateResponse<string>> GetStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(new ActorStateResponse<string>("5", null));
        }

        public Task SaveStateTransactionallyAsync(string actorType, string actorId, string data, CancellationToken cancellationToken = default)
        {
            Saves.Enqueue(data);
            return Task.CompletedTask;
        }

        public Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IActorResponseMessage> InvokeActorMethodWithRemotingAsync(ActorMessageSerializersManager serializersManager, IActorRequestMessage remotingRequestRequestMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RegisterReminderAsync(string actorType, string actorId, string reminderName, string data, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<HttpResponseMessage> GetReminderAsync(string actorType, string actorId, string reminderName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UnregisterReminderAsync(string actorType, string actorId, string reminderName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RegisterTimerAsync(string actorType, string actorId, string timerName, string data, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UnregisterTimerAsync(string actorType, string actorId, string timerName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
