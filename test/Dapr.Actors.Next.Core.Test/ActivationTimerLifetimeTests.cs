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
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class ActivationTimerLifetimeTests
{
    private static readonly ActorId Id = new("lifetime");

    [Theory]
    [InlineData("none")]
    [InlineData("deactivate")]
    [InlineData("dispose")]
    public async Task Deactivation_cleans_timers_even_on_failure_and_preserves_reminders(string failure)
    {
        var probe = new Probe(failure);
        await using var runtime = CreateRuntime(probe);
        var call = runtime.InvokeAsync("Lifetime", Id, "Start");
        await runtime.RunToIdle();
        await call;
        var deactivation = runtime.DeactivateAsync("Lifetime", Id);
        await runtime.RunToIdle();
        if (failure == "none") await deactivation;
        else await Assert.ThrowsAsync<InvalidOperationException>(() => deactivation);

        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(0, probe.TimerCalls);
        Assert.Equal(1, probe.Activations);
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(1, probe.ReminderCalls);
        Assert.Equal(2, probe.Activations);

        call = runtime.InvokeAsync("Lifetime", Id, "Start");
        await runtime.RunToIdle();
        await call;
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(1, probe.TimerCalls);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("factory")]
    public async Task Failed_activation_cleans_timers_without_starting_another_activation(string failure)
    {
        var probe = new Probe(failure);
        await using var runtime = CreateRuntime(probe);
        var call = runtime.InvokeAsync("Lifetime", Id, "Start");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<InvalidOperationException>(() => call);
        runtime.Time.Advance(TimeSpan.FromSeconds(5));
        await runtime.RunToIdle();
        Assert.Equal(1, probe.Activations);
        Assert.Equal(0, probe.TimerCalls);
    }

    private static ActorTestRuntime CreateRuntime(Probe probe) => new(services =>
        services.AddDaprActorsCore(registrations => registrations.Add(
            "Lifetime", typeof(IActor), typeof(LifetimeActor),
            (sp, _) =>
            {
                var actor = new LifetimeActor(sp.GetRequiredService<ActorActivationContext>(),
                    sp.GetRequiredService<IActorTimerScheduler>(),
                    sp.GetRequiredService<IActorReminderScheduler>(), probe, ++probe.Activations);
                if (actor.First && probe.Failure == "factory")
                {
                    actor.ScheduleTimer().GetAwaiter().GetResult();
                    throw new InvalidOperationException("factory failed");
                }
                return actor;
            }, new Dispatcher(), new ActorLifecycle(
                async (instance, _) =>
                {
                    var actor = (LifetimeActor)instance;
                    if (actor.First && probe.Failure == "activate")
                    {
                        await actor.ScheduleTimer();
                        throw new InvalidOperationException("activation failed");
                    }
                },
                async (instance, _) =>
                {
                    var actor = (LifetimeActor)instance;
                    if (actor.First)
                    {
                        await actor.ScheduleTimer();
                        if (probe.Failure == "deactivate")
                            throw new InvalidOperationException("deactivation failed");
                    }
                },
                static (_, _, _) => ValueTask.CompletedTask,
                static (_, _, _, _) => ValueTask.CompletedTask))));

    private sealed class Probe(string failure)
    {
        public string Failure { get; } = failure;
        public int Activations;
        public int TimerCalls;
        public int ReminderCalls;
    }

    private sealed class LifetimeActor(ActorActivationContext context, IActorTimerScheduler timers,
        IActorReminderScheduler reminders, Probe probe, int activation) : IActor, IDisposable
    {
        public bool First => activation == 1;
        public ValueTask ScheduleTimer() => timers.ScheduleAsync("Lifetime", context.ActorId, "timer",
            TimeSpan.FromSeconds(1), "Timer", "", period: TimeSpan.FromSeconds(1));
        public async Task Invoke(string operation)
        {
            if (operation == "Start")
            {
                await ScheduleTimer();
                await reminders.ScheduleAsync("Lifetime", context.ActorId, "Reminder",
                    TimeSpan.FromSeconds(2), TimeSpan.Zero, "");
            }
            else if (operation == "Timer") probe.TimerCalls++;
            else if (operation == "Reminder") probe.ReminderCalls++;
            else throw new InvalidOperationException(operation);
        }
        public void Dispose()
        {
            if (First && probe.Failure == "dispose")
                throw new InvalidOperationException("disposal failed");
        }
    }

    private sealed class Dispatcher : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            await ((LifetimeActor)actor).Invoke(request.MethodName);
            return new ActorDispatchResponse(null);
        }
    }
}
