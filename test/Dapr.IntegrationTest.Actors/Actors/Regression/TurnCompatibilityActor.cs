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

using System.Collections.Concurrent;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Regression;

// Out-of-band gates let the test release actor work without acquiring another actor turn.
public sealed class TurnCompatibilityProbe
{
    public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ScheduleFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConcurrentQueue<string> Events { get; } = new();
    public ConcurrentQueue<byte[]> Payloads { get; } = new();
    public int CallsFromCallbacks;
}

public sealed class TurnCompatibilityActor(ActorHost host, TurnCompatibilityProbe probe)
    : Actor(host), ITurnCompatibilityActor, IRemindable
{
    private const string ScheduleName = "turn-compatibility";
    private readonly Guid instanceId = Guid.NewGuid();
    private CachedValue? previousRead;

    public sealed class CachedValue
    {
        public int Value { get; set; }
    }

    protected override async Task OnActivateAsync()
    {
        await StateManager.GetOrAddStateAsync("value", new CachedValue());
        await StateManager.GetOrAddStateAsync("sentinel", "keep");
        await StateManager.GetOrAddStateAsync("callbacks", 0);
    }

    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task Reenter(ReentrantTurnRequest request, CancellationToken cancellationToken = default)
    {
        var index = request.Index;
        probe.Events.Enqueue($"enter:{index}");
        if (index == request.ActorIds.Length - 1)
        {
            probe.Held.TrySetResult();
            await probe.Release.Task;
        }
        else
        {
            var next = new ReentrantTurnRequest
            {
                ActorIds = request.ActorIds,
                Index = index + 1,
                UseRemoting = request.UseRemoting
            };
            var nextId = new ActorId(next.ActorIds[next.Index]);
            if (request.UseRemoting)
            {
                var proxy = ProxyFactory.CreateActorProxy<ITurnCompatibilityActor>(nextId, nameof(TurnCompatibilityActor));
                await proxy.Reenter(next, cancellationToken);
            }
            else
            {
                var proxy = ProxyFactory.Create(nextId, nameof(TurnCompatibilityActor));
                await proxy.InvokeMethodAsync(nameof(Reenter), next, cancellationToken);
            }
        }

        probe.Events.Enqueue($"exit:{index}");
    }

    public async Task<int> Increment(CancellationToken cancellationToken = default)
    {
        probe.Events.Enqueue($"increment:{Id}");
        var current = await StateManager.GetStateAsync<CachedValue>("value", cancellationToken);
        // Yield between read and write so concurrent turns can expose lost updates.
        await Task.Yield();
        var next = new CachedValue { Value = current.Value + 1 };
        await StateManager.SetStateAsync("value", next, cancellationToken);
        return next.Value;
    }

    public async Task<TurnStateSnapshot> Read(bool reload, CancellationToken cancellationToken = default)
    {
        if (reload)
        {
            await StateManager.ClearCacheAsync(cancellationToken);
        }

        var current = await StateManager.GetStateAsync<CachedValue>("value", cancellationToken);
        var secondRead = await StateManager.GetStateAsync<CachedValue>("value", cancellationToken);
        var snapshot = new TurnStateSnapshot
        {
            InstanceId = instanceId,
            Value = current.Value,
            SameWithinTurn = ReferenceEquals(current, secondRead),
            SameAsPreviousRead = ReferenceEquals(current, previousRead),
            TransientExists = await StateManager.ContainsStateAsync("transient", cancellationToken),
            SentinelExists = await StateManager.ContainsStateAsync("sentinel", cancellationToken),
            CallbackCount = await StateManager.GetStateAsync<int>("callbacks", cancellationToken)
        };
        previousRead = current;
        return snapshot;
    }

    public async Task FailAfterChangingState(CancellationToken cancellationToken = default)
    {
        await StateManager.SetStateAsync("value", new CachedValue { Value = -1 }, cancellationToken);
        await StateManager.AddStateAsync("transient", "discard", cancellationToken);
        await StateManager.RemoveStateAsync("sentinel", cancellationToken);
        throw new InvalidOperationException("Expected turn compatibility failure.");
    }

    public async Task StartSchedule(TurnScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, Host.JsonSerializerOptions);
        if (request.Reminder)
        {
            await RegisterReminderAsync(ScheduleName, bytes, TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
        }
        else
        {
            await RegisterTimerAsync(ScheduleName, nameof(TimerCallback), bytes,
                TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
        }
    }

    public Task StopSchedule(bool reminder, CancellationToken cancellationToken = default) => reminder
        ? UnregisterReminderAsync(ScheduleName)
        : UnregisterTimerAsync(ScheduleName);

    public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period) =>
        RunCallbackAsync(state);

    private Task TimerCallback(byte[] state) => RunCallbackAsync(state);

    private async Task RunCallbackAsync(byte[] bytes)
    {
        var request = JsonSerializer.Deserialize<TurnScheduleRequest>(bytes, Host.JsonSerializerOptions)!;
        var count = await StateManager.GetStateAsync<int>("callbacks") + 1;
        probe.Events.Enqueue($"callback-enter:{count}");
        probe.Payloads.Enqueue(request.Payload);
        if (count == 1)
        {
            probe.Held.TrySetResult();
            await probe.Release.Task;
        }

        var other = ProxyFactory.CreateActorProxy<ITurnCompatibilityActor>(
            new ActorId(request.CallbackActorId), nameof(TurnCompatibilityActor));
        await other.Ping();
        Interlocked.Increment(ref probe.CallsFromCallbacks);

        await StateManager.SetStateAsync("callbacks", count);
        if (count == 2)
        {
            await StopSchedule(request.Reminder);
        }

        probe.Events.Enqueue($"callback-exit:{count}");
        if (count == 2)
        {
            probe.ScheduleFinished.TrySetResult();
        }
    }
}
