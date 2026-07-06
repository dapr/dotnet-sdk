using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Timers;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.StateMachine.Test;

public sealed class StateMachineEdgeTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public async Task Internal_raise_async_entry_and_exit_run_in_order()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("edge-order");

        Assert.Equal("begun", await Invoke<string>(runtime, id, "Begin"));

        var data = await Invoke<EdgeData>(runtime, id, "ReadData");
        Assert.Equal(["exit-a", "begin", "entry-b", "follow"], data.Log);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Named_effect_and_guard_fail_without_registry()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("named");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke<string>(runtime, id, "UseNamedEffect"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke<string>(runtime, id, "UseNamedGuard"));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Custom_timer_schedule_and_cancel_use_virtual_time()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("timer");

        Assert.Equal("scheduled", await Invoke<string>(runtime, id, "ScheduleCustomTimer"));
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Equal(["timer:custom"], (await Invoke<EdgeData>(runtime, id, "ReadData")).Log);

        var canceled = ActorId.Create("canceled");
        Assert.Equal("canceled", await Invoke<string>(runtime, canceled, "CancelCustomTimer"));
        runtime.Time.Advance(TimeSpan.FromSeconds(1));
        await runtime.RunToIdle();
        Assert.Empty((await Invoke<EdgeData>(runtime, canceled, "ReadData")).Log);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Reply_and_no_branch_edges_are_reported_consistently()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("reply");

        Assert.Null(await Invoke<string>(runtime, id, "NoBranch"));
        Assert.Null(await Invoke<object?>(runtime, id, "NullReply"));
        await Assert.ThrowsAsync<InvalidCastException>(() => Invoke<int>(runtime, id, "WrongReply"));
        Assert.Equal(4, await Invoke<int>(runtime, id, "TableStateCount"));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Unhandled_without_fallback_and_bad_timer_payload_throw()
    {
        await using var runtime = CreateRuntime();
        var id = ActorId.Create("unhandled");

        await Assert.ThrowsAsync<InvalidActorEventException>(() => Invoke<object?>(runtime, id, "Unknown"));
        var malformed = runtime.InvokeAsync("Edge", id, StateMachineConstants.TimerOperationName, "{");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<JsonException>(() => malformed);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Global_fallback_can_reply_and_stale_timeout_is_ignored()
    {
        await using var runtime = CreateRuntime();
        var fallback = await Invoke<string>(runtime, ActorId.Create("fallback"), "Unknown", actorType: "Fallback");
        Assert.Equal("fallback", fallback);

        var id = ActorId.Create("stale-timeout");
        var stalePayload = JsonSerializer.Serialize(new { Name = StateMachineConstants.StateTimeoutTimerName, State = EdgeState.B.ToString() });
        var stale = runtime.InvokeAsync("Edge", id, StateMachineConstants.TimerOperationName, stalePayload);
        await runtime.RunToIdle();
        var staleBytes = await stale;
        Assert.NotNull(staleBytes);
        Assert.Empty(staleBytes);
        Assert.Empty((await Invoke<EdgeData>(runtime, id, "ReadData")).Log);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Missing_initial_state_fails_on_activation()
    {
        await using var runtime = CreateRuntime();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke<string>(runtime, ActorId.Create("missing"), "Begin", actorType: "MissingInitial"));
        Assert.Contains("initial state", ex.Message);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Builder_and_analyzer_cover_named_table_shapes()
    {
        var table = StateMachineActor<EdgeState, EdgeData>.BuildDefinitionFor(typeof(EdgeActor));

        Assert.Contains(table.States, state => state.Entry.Any(entry => entry.Name == "named-entry"));
        Assert.Contains(table.Transitions.SelectMany(transition => transition.Branches), branch => branch.Guard == "named-guard");
        Assert.Contains(StateMachineAnalyzer.Analyze(typeof(EdgeActor)).StructuralDefects, defect => defect.Contains("no reachable branch", StringComparison.Ordinal));
        Assert.Contains(StateMachineAnalyzer.Analyze(typeof(MissingInitialActor)).StructuralDefects, defect => defect.Contains("Initial state", StringComparison.Ordinal));
        Assert.Contains(StateMachineAnalyzer.Analyze(typeof(CycleActor)).StructuralDefects, defect => defect.Contains("cycle", StringComparison.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateMachineBuilder<EdgeState, EdgeData>().In(EdgeState.A).After(TimeSpan.FromTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => StateMachineAnalyzer.Analyze(typeof(object)));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Actor_constructor_validates_required_runtime_services()
    {
        var context = (ActorActivationContext)RuntimeHelpers.GetUninitializedObject(typeof(ActorActivationContext));
        var scheduler = new FakeTimerScheduler();

        Assert.Throws<ArgumentNullException>(() => new EdgeActor(null!, scheduler));
        Assert.Throws<ArgumentNullException>(() => new EdgeActor(context, null!));
    }

    private static ActorTestRuntime CreateRuntime() =>
        new(services => services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "Edge",
                typeof(IEdgeActor),
                typeof(EdgeActor),
                (sp, _) => new EdgeActor(sp.GetRequiredService<ActorActivationContext>(), sp.GetRequiredService<IActorTimerScheduler>()),
                new EdgeActorDispatcher(),
                Lifecycle());
            registrations.Add(
                "MissingInitial",
                typeof(IMissingInitialActor),
                typeof(MissingInitialActor),
                (sp, _) => new MissingInitialActor(sp.GetRequiredService<ActorActivationContext>(), sp.GetRequiredService<IActorTimerScheduler>()),
                new EdgeActorDispatcher(),
                Lifecycle());
            registrations.Add(
                "Fallback",
                typeof(IFallbackActor),
                typeof(FallbackActor),
                (sp, _) => new FallbackActor(sp.GetRequiredService<ActorActivationContext>(), sp.GetRequiredService<IActorTimerScheduler>()),
                new EdgeActorDispatcher(),
                Lifecycle());
        }));

    private static ActorLifecycle Lifecycle() =>
        new(
            (actor, cancellationToken) => ((Actor)actor).InvokeOnActivateAsync(cancellationToken),
            (actor, cancellationToken) => ((Actor)actor).InvokeOnDeactivateAsync(cancellationToken),
            (actor, context, cancellationToken) => ((Actor)actor).InvokeOnPreActorMethodAsync(context, cancellationToken),
            (actor, context, exception, cancellationToken) => ((Actor)actor).InvokeOnPostActorMethodAsync(context, exception, cancellationToken));

    private static async Task<T> Invoke<T>(ActorTestRuntime runtime, ActorId id, string operation, object? value = null, string actorType = "Edge")
    {
        var payload = value is null ? string.Empty : JsonSerializer.Serialize(value);
        var call = runtime.InvokeAsync(actorType, id, operation, payload);
        await runtime.RunToIdle();
        var bytes = await call;
        if (bytes is null || bytes.Length == 0)
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes))!;
    }

    private sealed class FakeTimerScheduler : IActorTimerScheduler
    {
        public ValueTask ScheduleAsync(string actorType, ActorId actorId, string name, TimeSpan dueTime, string operationName, string argumentsJson, TimeSpan? period = null, TimeSpan? ttl = null, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask RescheduleAsync(string actorType, ActorId actorId, string name, TimeSpan dueTime, string operationName, string argumentsJson, TimeSpan? period = null, TimeSpan? ttl = null, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask CancelAsync(string actorType, ActorId actorId, string name, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
