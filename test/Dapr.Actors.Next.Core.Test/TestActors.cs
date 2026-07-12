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

namespace Dapr.Actors.Next.Core.Test;

public interface ICounterActor : IActor
{
}

public interface IOtherCounterActor : IActor
{
}

public interface IDynamicCounterActor : IActor
{
}

public sealed class CounterState
{
    public int Value { get; set; }
}

public sealed class ScopedProbe : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class CounterActor : Actor, IDisposable
{
    private readonly ActorActivationContext context;
    private readonly IActorInvocationClient invocationClient;
    private readonly ScopedProbe probe;
    private readonly List<string> events;

    public CounterActor(ActorActivationContext context, IActorInvocationClient invocationClient, ScopedProbe probe, List<string> events)
    {
        this.context = context;
        this.invocationClient = invocationClient;
        this.probe = probe;
        this.events = events;
    }

    public List<string> Events => events;

    public bool Disposed { get; private set; }

    public ScopedProbe Probe => probe;

    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task<int> IncrementAsync(int amount, CancellationToken cancellationToken)
    {
        Events.Add("method");
        var state = await State.GetOrCreateAsync("state", () => new CounterState(), cancellationToken);
        state.Value.Value += amount;
        return state.Value.Value;
    }

    public async Task<int> ReadAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new CounterState(), cancellationToken);
        return state.Value.Value;
    }

    public async Task<int> ReenterAsync(CancellationToken cancellationToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("2");
        var response = await invocationClient.InvokeAsync("Counter", Id.Value, "Increment", bytes, new Dictionary<string, string> { ["dapr-reentrant-id"] = "chain" }, cancellationToken);
        return int.Parse(System.Text.Encoding.UTF8.GetString(response!));
    }

    public async Task<int> ReenterXAsync(CancellationToken cancellationToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("2");
        var response = await invocationClient.InvokeAsync("Counter", Id.Value, "Increment", bytes, new Dictionary<string, string> { ["x-dapr-reentrant-id"] = "chain-x" }, cancellationToken);
        return int.Parse(System.Text.Encoding.UTF8.GetString(response!));
    }

    public ValueTask ActivateAsync(CancellationToken cancellationToken)
    {
        Events.Add("activate");
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken)
    {
        Events.Add("deactivate");
        return ValueTask.CompletedTask;
    }

    public ValueTask PreAsync(ActorMethodContext context, CancellationToken cancellationToken)
    {
        Events.Add("pre");
        return ValueTask.CompletedTask;
    }

    public ValueTask PostAsync(ActorMethodContext context, Exception? exception, CancellationToken cancellationToken)
    {
        Events.Add(exception is null ? "post" : "post-ex");
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        Disposed = true;
    }
}

public sealed class CounterDispatcher : IActorDispatcher
{
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var counter = (CounterActor)actor;
        return request.MethodName switch
        {
            "Increment" => Text((await counter.IncrementAsync(int.Parse(System.Text.Encoding.UTF8.GetString(request.Payload.Span)), cancellationToken)).ToString()),
            "Read" => Text((await counter.ReadAsync(cancellationToken)).ToString()),
            "Reenter" => Text((await counter.ReenterAsync(cancellationToken)).ToString()),
            "ReenterX" => Text((await counter.ReenterXAsync(cancellationToken)).ToString()),
            _ => throw new InvalidOperationException("Unknown method."),
        };
    }

    private static ActorDispatchResponse Text(string value) => new(System.Text.Encoding.UTF8.GetBytes(value));
}
