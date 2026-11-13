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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dapr.Actors.Runtime;

public sealed class ActorTurnConcurrencyTests
{
    [Theory]
    [InlineData("Activation")]
    [InlineData("Pre")]
    [InlineData("Method")]
    [InlineData("Post")]
    [InlineData("Save")]
    [InlineData("Failure")]
    [InlineData("Clear")]
    public async Task CancelledRequestRetainsTurnThroughLifecycleAndStateCleanup(string stage)
    {
        var probe = new Probe(stage);
        var manager = CreateManager(probe);
        var id = ActorId.CreateRandom();
        using var cancellation = new CancellationTokenSource();
        var first = DispatchAsync(manager, id, cancellation.Token);
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Task second = null;
        try
        {
            await cancellation.CancelAsync();
            second = DispatchAsync(manager, id, TestContext.Current.CancellationToken);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            probe.Release.TrySetResult(true);
            if (stage == "Failure" || stage == "Clear")
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            }
            else
            {
                await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }

            if (second != null)
            {
                await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        }

        Assert.Equal(1, probe.CreateCount);
        Assert.Equal(2, probe.MethodCalls);
    }

    [Theory]
    [InlineData("Method")]
    [InlineData("Post")]
    [InlineData("Failure")]
    public async Task DeactivationWaitsForEntireTurn(string stage)
    {
        var probe = new Probe(stage);
        var manager = CreateManager(probe);
        var id = ActorId.CreateRandom();
        var first = DispatchAsync(manager, id, TestContext.Current.CancellationToken);
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var deactivation = manager.DeactivateActorAsync(id);
        try
        {
            Assert.False(deactivation.IsCompleted);
            Assert.Equal(0, probe.DeleteCount);
        }
        finally
        {
            probe.Release.TrySetResult(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            await deactivation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, probe.DeleteCount);
        Assert.False(manager.TryGetActorAsync(id, out _));
        await DispatchAsync(manager, id, TestContext.Current.CancellationToken);
        Assert.Equal(2, probe.CreateCount);
    }

    [Fact]
    public async Task DispatchWaitsForDeactivationAndDisposalBeforeReactivation()
    {
        var probe = new Probe("Delete");
        var manager = CreateManager(probe);
        var id = ActorId.CreateRandom();
        await DispatchAsync(manager, id, TestContext.Current.CancellationToken);
        var deactivation = manager.DeactivateActorAsync(id);
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var next = DispatchAsync(manager, id, TestContext.Current.CancellationToken);
        try
        {
            Assert.False(next.IsCompleted);
            Assert.Equal(1, probe.CreateCount);
        }
        finally
        {
            probe.Release.TrySetResult(true);
            await deactivation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, probe.CreateCount);
    }

    [Theory]
    [InlineData("Reminder")]
    [InlineData("Timer")]
    public async Task CallbacksWaitForRunningMethod(string callback)
    {
        var probe = new Probe("Method");
        var manager = CreateManager(probe);
        var id = ActorId.CreateRandom();
        var first = DispatchAsync(manager, id, TestContext.Current.CancellationToken);
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(callback == "Timer"
            ? "{\"callback\":\"TimerCallback\"}"
            : "{\"data\":\"\",\"dueTime\":\"0h0m0s0ms\",\"period\":\"0h0m1s0ms\"}"));
        var next = callback == "Timer"
            ? (Task)manager.FireTimerAsync(id, body, cancellationToken: TestContext.Current.CancellationToken)
            : manager.FireReminderAsync(id, "test", body, cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            Assert.False(next.IsCompleted);
            Assert.Equal(0, probe.CallbackCalls);
        }
        finally
        {
            probe.Release.TrySetResult(true);
            await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, probe.CallbackCalls);
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, null, false)]
    public async Task ReentrancyHonorsGlobalAndPerTypeConfiguration(bool globalEnabled, bool? typeEnabled,
        bool canReenter)
    {
        var probe = new Probe("Method");
        var manager = CreateManager(probe, globalEnabled, typeEnabled);
        var id = ActorId.CreateRandom();
        var first = DispatchAsync(manager, id, TestContext.Current.CancellationToken, "chain");
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var nested = DispatchAsync(manager, id, TestContext.Current.CancellationToken, "chain");
        try
        {
            if (canReenter)
            {
                await nested.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            else
            {
                Assert.False(nested.IsCompleted);
            }
        }
        finally
        {
            probe.Release.TrySetResult(true);
            await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await nested.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    private static async Task DispatchAsync(ActorManager manager, ActorId id, CancellationToken cancellationToken,
        string reentrancyId = null)
    {
        ActorReentrancyContextAccessor.ReentrancyContext = reentrancyId;
        try
        {
            using var input = new MemoryStream();
            using var output = new MemoryStream();
            await manager.DispatchWithoutRemotingAsync(id, nameof(IConcurrencyActor.Execute), input, output,
                null, cancellationToken);
        }
        finally
        {
            ActorReentrancyContextAccessor.ReentrancyContext = null;
        }
    }

    private static ActorManager CreateManager(Probe probe, bool globalEnabled = false, bool? typeEnabled = null)
    {
        var typeOptions = typeEnabled.HasValue
            ? new ActorRuntimeOptions { ReentrancyConfig = new ActorReentrancyConfig { Enabled = typeEnabled.Value } }
            : null;
        var registration =
            new ActorRegistration(ActorTypeInformation.Get(typeof(ConcurrencyActor), actorTypeName: null), typeOptions);
        var interactor = new DaprHttpInteractor(null, "http://localhost:3500", null, null);
        return new ActorManager(registration, new TestActivator(probe), JsonSerializerDefaults.Web, false,
            NullLoggerFactory.Instance, ActorProxy.DefaultProxyFactory, interactor,
            new ActorReentrancyConfig { Enabled = globalEnabled });
    }

    public interface IConcurrencyActor : IActor
    {
        Task Execute();
    }

    public sealed class Probe(string stage)
    {
        private int blocked;
        public string Stage => stage;
        public int CreateCount;
        public int DeleteCount;
        public int MethodCalls;
        public int CallbackCalls;
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PauseAsync(string currentStage)
        {
            if (currentStage != stage || Interlocked.Exchange(ref this.blocked, 1) != 0)
            {
                return Task.CompletedTask;
            }

            this.Entered.SetResult(true);
            return this.Release.Task;
        }
    }

    private sealed class TestActivator(Probe probe) : ActorActivator
    {
        public override Task<ActorActivatorState> CreateAsync(ActorHost host)
        {
            Interlocked.Increment(ref probe.CreateCount);
            return Task.FromResult(new ActorActivatorState(new ConcurrencyActor(host, probe)));
        }

        public override async Task DeleteAsync(ActorActivatorState state)
        {
            await probe.PauseAsync("Delete");
            Interlocked.Increment(ref probe.DeleteCount);
        }
    }

    public sealed class ConcurrencyActor : Actor, IConcurrencyActor, IRemindable
    {
        private readonly Probe probe;

        public ConcurrencyActor(ActorHost host, Probe probe) : base(host)
        {
            this.probe = probe;
            var state = new Mock<IActorStateManager>();
            state.Setup(s => s.SaveStateAsync(It.IsAny<CancellationToken>()))
                .Returns(() => probe.MethodCalls > 0 ? probe.PauseAsync("Save") : Task.CompletedTask);
            state.Setup(s => s.ClearCacheAsync(It.IsAny<CancellationToken>()))
                .Returns(() => probe.MethodCalls > 0 ? probe.PauseAsync("Clear") : Task.CompletedTask);
            this.StateManager = state.Object;
        }

        public async Task Execute()
        {
            if (Interlocked.Increment(ref this.probe.MethodCalls) == 1 &&
                (this.probe.Stage == "Failure" || this.probe.Stage == "Clear"))
            {
                throw new InvalidOperationException("Expected failure for cleanup isolation.");
            }

            await this.probe.PauseAsync("Method");
        }

        protected override Task OnActivateAsync() => this.probe.PauseAsync("Activation");
        protected override Task OnPreActorMethodAsync(ActorMethodContext context) => this.probe.PauseAsync("Pre");
        protected override Task OnPostActorMethodAsync(ActorMethodContext context) => this.probe.PauseAsync("Post");

        protected override Task OnActorMethodFailedAsync(ActorMethodContext context, Exception exception) =>
            this.probe.PauseAsync("Failure");

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref this.probe.CallbackCalls);
            return Task.CompletedTask;
        }

        public Task TimerCallback()
        {
            Interlocked.Increment(ref this.probe.CallbackCalls);
            return Task.CompletedTask;
        }
    }
}
