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
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(6, await IncrementAsync("a2"));
        var second = GetActor();
        first.Release.SetResult();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Contains("replaced", ex.Message);
        Assert.NotSame(first, second);
        Assert.Same(second, GetActor());
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
        Assert.Equal(7, await IncrementAsync("a2"));
    }

    [Fact]
    public async Task Method_DeactivatedWhileRunning_SkipsSaveAndFails()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = HoldAsync("a1");
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await manager.DeactivateActorAsync(id);
        first.Release.SetResult();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Contains("deactivated", ex.Message);
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
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(6, await IncrementAsync("a2"));
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
    }

    [Fact]
    public async Task Timer_ReplacedWhileRunning_SkipsSaveAndFails()
    {
        await IncrementAsync("a1");
        var first = GetActor();

        var held = FireTimerAsync("a1", nameof(CountingActor.HoldTimerAsync));
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(6, await IncrementAsync("a2"));
        first.Release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
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
        await first.Held.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(7, await IncrementAsync(otherActivationId));
        first.Release.SetResult();

        Assert.Equal(100, await held);
        Assert.Same(first, GetActor());
        Assert.Equal(3, store.Saves.Count);
        Assert.Contains("100", store.Saves.Last());
    }

    [Fact]
    public async Task Method_ReplacedDuringPostMethodHook_SkipsSaveAndFails()
    {
        Assert.Equal(6, await IncrementAsync("a1"));
        var first = GetActor();
        first.HoldInPostHook = true;

        var held = IncrementAsync("a1");
        await first.PostHeld.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(6, await IncrementAsync("a2"));
        first.PostRelease.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => held);
        Assert.NotSame(first, GetActor());
        Assert.Equal(2, store.Saves.Count);
        Assert.DoesNotContain(store.Saves, s => s.Contains("100"));
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
    public async Task ConcurrentFirstActivation_LateDifferentId_FailsAndKeepsNewer()
    {
        var race = await StartActivationRaceAsync("a1", "a2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => race.Late);

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.Same(race.Newer, registered);
        Assert.Equal(race.Acknowledged, race.Store.Saves.Count);
        Assert.Equal(7, await DispatchAsync(race.Manager, nameof(ICountingActor.IncrementAsync), "a2"));
        Assert.Contains("\"value\":7}", race.Store.Saves.Last());
    }

    [Theory]
    [InlineData("a2")]
    [InlineData(null)]
    [InlineData("")]
    public async Task ConcurrentFirstActivation_LateSameOrMissingId_ReusesNewer(string lateId)
    {
        var race = await StartActivationRaceAsync(lateId, "a2");

        Assert.Equal(7, await race.Late);

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.Same(race.Newer, registered);
        Assert.DoesNotContain(race.Store.Saves.Skip(race.Acknowledged), s => s.Contains("\"value\":5}"));
        Assert.Contains("\"value\":7}", race.Store.Saves.Last());
    }

    [Theory]
    [InlineData("a2")]
    [InlineData("a1")]
    public async Task ConcurrentFirstActivation_DiscardedOnDeactivateThrows_IsLogged(string lateId)
    {
        var logs = new CapturingLoggerFactory();
        var race = await StartActivationRaceAsync(lateId, "a2", discardedThrowsOnDeactivate: true, logs);

        if (lateId == "a2")
        {
            Assert.Equal(7, await race.Late);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => race.Late);
        }

        Assert.True(race.Manager.TryGetActorAsync(id, out var registered));
        Assert.Same(race.Newer, registered);

        var error = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.IsType<InvalidTimeZoneException>(error.Exception);
        Assert.StartsWith("Deactivating discarded activation", error.Message);
    }

    private async Task<(ActorManager Manager, StateStore Store, Task<int> Late, Actor Newer, int Acknowledged)> StartActivationRaceAsync(string lateId, string newerId, bool discardedThrowsOnDeactivate = false, ILoggerFactory loggerFactory = null)
    {
        var raceStore = new StateStore();
        var registration = new ActorRegistration(ActorTypeInformation.Get(typeof(GatedActivateActor), actorTypeName: null));
        var raceManager = new ActorManager(registration, new DefaultActorActivator(), JsonSerializerDefaults.Web, false, loggerFactory ?? NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory, raceStore);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        GatedActivateActor.Gate = (started, release, discardedThrowsOnDeactivate);

        var late = DispatchAsync(raceManager, nameof(ICountingActor.IncrementAsync), lateId);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(6, await DispatchAsync(raceManager, nameof(ICountingActor.IncrementAsync), newerId));
        Assert.True(raceManager.TryGetActorAsync(id, out var newer));
        var acknowledged = raceStore.Saves.Count;

        release.SetResult();
        return (raceManager, raceStore, late, newer, acknowledged);
    }

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

        public bool HoldInPostHook { get; set; }

        public TaskCompletionSource PostHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PostRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowOnDeactivate { get; set; }

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
            return 100;
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

        public void Dispose() => IsDisposed = true;

        protected override Task OnActivateAsync()
        {
            IsActivated = true;
            return Task.CompletedTask;
        }

        protected override Task OnDeactivateAsync()
        {
            IsDeactivated = true;
            return ThrowOnDeactivate ? throw new InvalidOperationException("deactivate failed") : Task.CompletedTask;
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

        public static (TaskCompletionSource Started, TaskCompletionSource Release, bool ThrowOnDeactivate)? Gate { get; set; }

        private bool throwOnDeactivate;

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
            if (gate is { } g)
            {
                throwOnDeactivate = g.ThrowOnDeactivate;
                g.Started.SetResult();
                await g.Release.Task;
            }
        }

        protected override Task OnDeactivateAsync() =>
            throwOnDeactivate ? throw new InvalidTimeZoneException("deactivate failed") : Task.CompletedTask;
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
