using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;

namespace Dapr.Actors.Next.Core.Test;

public interface INastyActor : IActor;

public sealed class NastyState
{
    public int Applied { get; set; }

    public HashSet<string> OperationIds { get; set; } = [];

    public List<string> Events { get; set; } = [];
}

public sealed class TurnSerializationProbe
{
    private readonly TaskCompletionSource<object?> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int active;
    private int maxActive;

    public Task Entered => entered.Task;

    public int MaxActive => Volatile.Read(ref maxActive);

    public void Release() => release.TrySetResult(null);

    public async Task HoldAsync(CancellationToken cancellationToken)
    {
        var current = Interlocked.Increment(ref active);
        UpdateMax(current);
        entered.TrySetResult(null);

        try
        {
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }

    private void UpdateMax(int current)
    {
        while (true)
        {
            var observed = Volatile.Read(ref maxActive);
            if (current <= observed || Interlocked.CompareExchange(ref maxActive, current, observed) == observed)
            {
                return;
            }
        }
    }
}

public sealed class NastyActor(ActorActivationContext context, IActorInvocationClient client, TurnSerializationProbe probe) : Actor, INastyActor
{
    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public async Task<int> StartReentrantAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        state.Value.Events.Add("outer-before");
        var response = await client.InvokeAsync(
            "Nasty",
            Id.Value,
            "ReentrantInner",
            ReadOnlyMemory<byte>.Empty,
            new Dictionary<string, string> { ["dapr-reentrant-id"] = "core-chain" },
            cancellationToken);
        state.Value.Events.Add("outer-after");
        return int.Parse(System.Text.Encoding.UTF8.GetString(response!));
    }

    public async Task<int> ReentrantInnerAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        state.Value.Applied++;
        state.Value.Events.Add("inner");
        return state.Value.Applied;
    }

    public async Task<int> ApplyOnceAsync(string operationId, CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        if (state.Value.OperationIds.Add(operationId))
        {
            state.Value.Applied++;
            state.Value.Events.Add($"apply:{operationId}");
        }

        return state.Value.Applied;
    }

    public async Task<int> HoldAndApplyAsync(string operationId, CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        await probe.HoldAsync(cancellationToken).ConfigureAwait(false);
        if (state.Value.OperationIds.Add(operationId))
        {
            state.Value.Applied++;
            state.Value.Events.Add($"apply:{operationId}");
        }

        return state.Value.Applied;
    }

    public async Task<int> HoldAsync(CancellationToken cancellationToken)
    {
        await probe.HoldAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    public async Task<int> ReadAppliedAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        return state.Value.Applied;
    }

    public async Task<int> TimerAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        if (state.Value.OperationIds.Add("timer"))
        {
            state.Value.Applied += 10;
            state.Value.Events.Add("timer");
        }

        return state.Value.Applied;
    }

    public async Task<int> ReminderAsync(CancellationToken cancellationToken)
    {
        var state = await State.GetOrCreateAsync("state", () => new NastyState(), cancellationToken);
        if (state.Value.OperationIds.Add("reminder"))
        {
            state.Value.Applied += 10;
            state.Value.Events.Add("reminder");
        }

        return state.Value.Applied;
    }
}

public sealed class NastyActorDispatcher : IActorDispatcher
{
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var nasty = (NastyActor)actor;
        var arguments = System.Text.Encoding.UTF8.GetString(request.Payload.Span);
        return request.MethodName switch
        {
            "StartReentrant" => Text((await nasty.StartReentrantAsync(cancellationToken)).ToString()),
            "ReentrantInner" => Text((await nasty.ReentrantInnerAsync(cancellationToken)).ToString()),
            "ApplyOnce" => Text((await nasty.ApplyOnceAsync(arguments.Trim('"'), cancellationToken)).ToString()),
            "HoldAndApply" => Text((await nasty.HoldAndApplyAsync(arguments.Trim('"'), cancellationToken)).ToString()),
            "Hold" => Text((await nasty.HoldAsync(cancellationToken)).ToString()),
            "ReadApplied" => Text((await nasty.ReadAppliedAsync(cancellationToken)).ToString()),
            "Timer" => Text((await nasty.TimerAsync(cancellationToken)).ToString()),
            "Reminder" => Text((await nasty.ReminderAsync(cancellationToken)).ToString()),
            _ => throw new InvalidOperationException("Unknown method."),
        };
    }

    private static ActorDispatchResponse Text(string value) => new(System.Text.Encoding.UTF8.GetBytes(value));
}
