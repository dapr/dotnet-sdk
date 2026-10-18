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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class InlineTurnCancellationTests
{
    [Fact]
    public async Task Cancellation_after_queue_admission_does_not_cancel_mailbox_scheduling()
    {
        using var cancellation = new CancellationTokenSource();
        var scheduler = new InlineDrainScheduler
        {
            AllowsInlineExecution = false,
            BeforeSchedule = cancellation.Cancel,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryActorAdapters();
        services.AddSingleton<IActorScheduler>(scheduler);
        services.AddDaprActorsCore(registrations => registrations.Add(
            "Cancellation", typeof(ICancellationActor), typeof(CancellationActor),
            (_, id) => new CancellationActor(id), new Dispatcher(new Probe(), false)));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var runtime = provider.GetRequiredService<IActorRuntime>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runtime.InvokeAsync("Cancellation", "one", "Next", ReadOnlyMemory<byte>.Empty,
                new Dictionary<string, string>(), cancellation.Token));
        Assert.Equal(1, scheduler.ScheduleCount);
        Assert.False(scheduler.LastToken.CanBeCanceled);

        var result = await runtime.InvokeAsync("Cancellation", "one", "Next", ReadOnlyMemory<byte>.Empty,
            new Dictionary<string, string>()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("next", System.Text.Encoding.UTF8.GetString(result!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_inline_turn_schedules_queued_work_without_masking_its_exception(bool throwOriginalError)
    {
        var probe = new Probe();
        var scheduler = new InlineDrainScheduler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryActorAdapters();
        services.AddSingleton<IActorScheduler>(scheduler);
        services.AddDaprActorsCore(registrations => registrations.Add(
            "Cancellation", typeof(ICancellationActor), typeof(CancellationActor),
            (_, id) => new CancellationActor(id), new Dispatcher(probe, throwOriginalError)));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var runtime = provider.GetRequiredService<IActorRuntime>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var firstCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using var secondCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);

        var first = runtime.InvokeAsync("Cancellation", "one", "Hold", ReadOnlyMemory<byte>.Empty,
            new Dictionary<string, string>(), firstCancellation.Token);
        try
        {
            await probe.Entered.Task.WaitAsync(timeout.Token);
            var second = runtime.InvokeAsync("Cancellation", "one", "Next", ReadOnlyMemory<byte>.Empty,
                new Dictionary<string, string>(), secondCancellation.Token);
            Assert.Equal(1, scheduler.ScheduleCount);
            Assert.False(second.IsCompleted);
            await firstCancellation.CancelAsync();
            Assert.False(second.IsCompleted);
            probe.Release.TrySetResult();

            if (throwOriginalError)
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => first);
                Assert.Equal("Original actor failure", exception.Message);
            }
            else
            {
                var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
                Assert.Equal(firstCancellation.Token, exception.CancellationToken);
            }

            Assert.Equal("next", System.Text.Encoding.UTF8.GetString(
                (await second.WaitAsync(TimeSpan.FromSeconds(2), timeout.Token))!));
            Assert.Equal(2, scheduler.ScheduleCount);
            Assert.False(scheduler.LastToken.CanBeCanceled);
        }
        finally
        {
            probe.Release.TrySetResult();
            await secondCancellation.CancelAsync();
        }
    }

    // Run the production mailbox pump synchronously to deterministically finish its
    // first, unsuccessful drain attempt while the inline turn still owns the slot.
    private sealed class InlineDrainScheduler : IActorScheduler
    {
        public bool AllowsInlineExecution { get; init; } = true;
        public Action? BeforeSchedule { get; init; }
        public int ScheduleCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public async ValueTask ScheduleAsync(IActorMailbox mailbox, CancellationToken cancellationToken = default)
        {
            this.BeforeSchedule?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            this.ScheduleCount++;
            this.LastToken = cancellationToken;
            var runtimeMailbox = Assert.IsType<RuntimeActorMailbox>(mailbox);
            if (runtimeMailbox.TryScheduleDrain(cancellationToken))
            {
                await runtimeMailbox.DrainScheduledAsync();
            }
        }
    }

    private sealed class Probe
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public interface ICancellationActor : IActor;

    private sealed class CancellationActor(ActorId id) : Actor, ICancellationActor
    {
        protected override ActorId Id => id;
        protected override IActorStateAccessor State => throw new NotSupportedException();
    }

    private sealed class Dispatcher(Probe probe, bool throwOriginalError) : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.MethodName == "Hold")
            {
                probe.Entered.TrySetResult();
                await probe.Release.Task;
                if (throwOriginalError)
                {
                    throw new InvalidOperationException("Original actor failure");
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new ActorDispatchResponse("next"u8.ToArray());
        }
    }
}
