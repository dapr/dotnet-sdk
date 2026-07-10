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
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.Testing.Test;

public interface ITestingActor : IActor;

public sealed class TestingState
{
    public int Value { get; set; }

    public List<string> Events { get; set; } = [];
}

public sealed class MigrationTestingStateV1
{
    public int Count { get; set; }
}

public sealed class MigrationTestingStateV2
{
    public int Quantity { get; set; }
}

public sealed class MigrationTestingStateV3
{
    public int Total { get; set; }

    public string Label { get; set; } = "";
}

public sealed class TestingActor(ActorActivationContext context, IActorInvocationClient client, IActorReminderScheduler reminders) : Actor, ITestingActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task<int> AddAsync(int amount, CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new TestingState(), cancellationToken);
        state.Value.Value += amount;
        state.Value.Events.Add($"add:{amount}");
        return state.Value.Value;
    }

    public async Task<int> TimerAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new TestingState(), cancellationToken);
        state.Value.Value += 10;
        state.Value.Events.Add("timer");
        return state.Value.Value;
    }

    public async Task<int> ReminderAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new TestingState(), cancellationToken);
        state.Value.Value += 100;
        state.Value.Events.Add("reminder");
        return state.Value.Value;
    }

    public async Task ScheduleReminderFromActorAsync(CancellationToken cancellationToken)
    {
        await reminders.ScheduleAsync("Testing", Id, "Reminder", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), string.Empty, overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task<int> ReenterAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new TestingState(), cancellationToken);
        state.Value.Events.Add("outer-before");
        var response = await client.InvokeAsync(
            "Testing",
            Id.Value,
            "Inner",
            ReadOnlyMemory<byte>.Empty,
            Dapr.Actors.Next.Core.ActorHeaders.Empty,
            cancellationToken);
        state.Value.Events.Add("outer-after");
        return int.Parse(System.Text.Encoding.UTF8.GetString(response!));
    }

    public async Task<int> SlowAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(25, cancellationToken);
        var state = await State.GetOrCreateAsync("state", () => new TestingState(), cancellationToken);
        state.Value.Events.Add("slow");
        return state.Value.Value;
    }

    public async Task<int> InnerAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new TestingState(), cancellationToken);
        state.Value.Value++;
        state.Value.Events.Add("inner");
        return state.Value.Value;
    }

    public async Task<int> ReadMigratedAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("migrating", () => new MigrationTestingStateV3(), cancellationToken);
        return state.Value.Total;
    }
}

public sealed class OddNamedActor(ActorActivationContext context) : Actor, ITestingActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public Task InvokeOnPurposeAsync() => Task.CompletedTask;
}

public sealed class TestingActorDispatcher : IActorDispatcher
{
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var testActor = (TestingActor)actor;
        return request.MethodName switch
        {
            "Add" => Text((await testActor.AddAsync(int.Parse(System.Text.Encoding.UTF8.GetString(request.Payload.Span)), cancellationToken)).ToString()),
            "Timer" => Text((await testActor.TimerAsync(cancellationToken)).ToString()),
            "Reminder" => Text((await testActor.ReminderAsync(cancellationToken)).ToString()),
            "ScheduleReminderFromActor" => await CompleteAsync(testActor.ScheduleReminderFromActorAsync(cancellationToken)),
            "Reenter" => Text((await testActor.ReenterAsync(cancellationToken)).ToString()),
            "Slow" => Text((await testActor.SlowAsync(cancellationToken)).ToString()),
            "Inner" => Text((await testActor.InnerAsync(cancellationToken)).ToString()),
            "ReadMigrated" => Text((await testActor.ReadMigratedAsync(cancellationToken)).ToString()),
            _ => throw new InvalidOperationException("Unknown method."),
        };
    }

    private static ActorDispatchResponse Text(string value) => new(System.Text.Encoding.UTF8.GetBytes(value));

    private static async ValueTask<ActorDispatchResponse> CompleteAsync(Task task)
    {
        await task.ConfigureAwait(false);
        return new ActorDispatchResponse(null);
    }
}
