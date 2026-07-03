using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.SourceGenerators.Sample;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Testing.Test;

public sealed class ActorTestRuntimeTests
{
    [Fact]
    public async Task Generated_proxy_runs_against_in_memory_runtime_and_exposes_state()
    {
        _ = typeof(CalculatorActor);
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddSingleton<CalculatorDependency>();
            services.AddDaprActors(_ => { });
        });

        var actor = runtime.CreateActor<ICalculatorActor>(ActorId.Create("calc-test"), "Calculator");
        var add = actor.AddAsync(new CalculationInput(3));
        await runtime.RunToIdle();
        await add;

        var state = runtime.StateOf(actor).Get<CalculatorState>("calculator");
        Assert.NotNull(state);
        Assert.Equal(4, state.Value);
    }

    [Fact]
    public async Task Same_seed_replays_same_interleaving_and_result()
    {
        var first = await RunTwoActorAdds(seed: 17);
        var second = await RunTwoActorAdds(seed: 17);

        Assert.Equal(first.Transcript, second.Transcript);
        Assert.Equal(first.Left, second.Left);
        Assert.Equal(first.Right, second.Right);
    }

    [Fact]
    public async Task Injected_state_write_fault_surfaces_and_next_turn_recovers_dirty_state()
    {
        await using var runtime = CreateTestingRuntime(new SeededRandomActorScheduler(1));
        runtime.Faults.FailNextStateWrite<TestingState>();

        var failed = runtime.InvokeAsync("Testing", ActorId.Create("fault"), "Add", "5");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => failed);

        var recovery = runtime.InvokeAsync("Testing", ActorId.Create("fault"), "Add", "0");
        await runtime.RunToIdle();
        var recovered = int.Parse(System.Text.Encoding.UTF8.GetString((await recovery)!));

        Assert.Equal(5, recovered);
    }

    [Fact]
    public async Task Virtual_time_fires_timer_and_reminder_in_due_time_order()
    {
        await using var runtime = CreateTestingRuntime(new PriorityActorScheduler(4));
        var id = ActorId.Create("time");
        runtime.Time.ScheduleReminder("Testing", id, "Reminder", TimeSpan.FromSeconds(2));
        runtime.Time.ScheduleTimer("Testing", id, "Timer", TimeSpan.FromSeconds(1));

        runtime.Time.Advance(TimeSpan.FromSeconds(2));
        await runtime.RunToIdle();

        var read = runtime.InvokeAsync("Testing", id, "Add", "0");
        await runtime.RunToIdle();
        Assert.Equal(110, int.Parse(System.Text.Encoding.UTF8.GetString((await read)!)));
        Assert.Equal(["Timer", "Reminder", "Add"], runtime.Transcript.Select(entry => entry.OperationName).ToArray());
    }

    [Fact]
    public async Task Reentrant_call_chain_completes_under_controlled_scheduler()
    {
        await using var runtime = CreateTestingRuntime(new SeededRandomActorScheduler(3));
        var call = runtime.InvokeAsync(
            "Testing",
            ActorId.Create("reentrant"),
            "Reenter",
            headers: new Dictionary<string, string> { ["dapr-reentrant-id"] = "test-chain" });

        await runtime.RunToIdle();

        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await call)!)));
        Assert.Contains(runtime.Transcript, entry => entry.OperationName == "Reenter");
    }

    [Fact]
    public async Task Failing_interleaving_replays_from_seed()
    {
        static async Task<IReadOnlyList<InterleavingTranscriptEntry>> RunFailure()
        {
            await using var runtime = CreateTestingRuntime(new SeededRandomActorScheduler(9));
            runtime.Faults.FailNextStateWrite<TestingState>();
            var failed = runtime.InvokeAsync("Testing", ActorId.Create("fail"), "Add", "1");
            await runtime.RunToIdle();
            await Assert.ThrowsAsync<ActorInjectedTransientException>(() => failed);
            return runtime.Scheduler.ReplayFromSeed().Seed == 9 ? runtime.Transcript : [];
        }

        var first = await RunFailure();
        var second = await RunFailure();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Structural_analysis_placeholder_reports_plain_actor_methods()
    {
        var analysis = ActorStateMachine.Analyze<TestingActor>();

        analysis.AssertNoStructuralDefects();
        Assert.Contains(analysis.ReachableMethods, method => method.Name == nameof(TestingActor.AddAsync));
        Assert.DoesNotContain(ActorStateMachine.Analyze<OddNamedActor>().ReachableMethods, method => method.Name == nameof(OddNamedActor.InvokeOnPurposeAsync));
        Assert.False(CoyoteBridge.IsEnabled);
    }

    [Fact]
    public async Task Fault_filters_only_consume_matching_faults_and_support_permanent_failures()
    {
        await using var runtime = CreateTestingRuntime(new SeededRandomActorScheduler(5));
        runtime.Faults.FailNextStateWrite<TestingState>(stateName: "other");
        var allowed = runtime.InvokeAsync("Testing", ActorId.Create("fault-filter"), "Add", "1");
        await runtime.RunToIdle();
        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await allowed)!)));

        runtime.Faults.FailNextStateWrite<TestingState>(transient: false);
        var failed = runtime.InvokeAsync("Testing", ActorId.Create("fault-filter-state"), "Add", "1");
        await runtime.RunToIdle();
        await Assert.ThrowsAsync<ActorInjectedPermanentException>(() => failed);

        runtime.Faults.FailNextInvocation(actorType: "Other", methodName: "Add");
        var notMatched = runtime.InvokeAsync("Testing", ActorId.Create("fault-filter"), "Add", "0");
        await runtime.RunToIdle();
        Assert.Equal(1, int.Parse(System.Text.Encoding.UTF8.GetString((await notMatched)!)));

        runtime.Faults.FailNextInvocation(transient: false, actorType: "Testing", methodName: "Add");
        void InvokeFault() => _ = runtime.InvokeAsync("Testing", ActorId.Create("fault-filter"), "Add", "0");
        Assert.IsType<ActorInjectedPermanentException>(Record.Exception((Action)InvokeFault));
    }

    [Fact]
    public async Task Runtime_and_scheduler_edges_are_observable()
    {
        await using var runtime = CreateTestingRuntime(new SeededRandomActorScheduler(6));

        Assert.False(await runtime.StepAsync());
        await runtime.RunToIdle();
        Assert.Throws<InvalidOperationException>(() => runtime.StateOf(new object()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => runtime.RunToIdle(new CancellationToken(canceled: true)));

        var scheduler = new SeededRandomActorScheduler(6);
        await scheduler.ScheduleAsync(new DummyMailbox());
        Assert.False(await scheduler.StepAsync());
        Assert.Empty(scheduler.Transcript);

        var inconsistent = new SeededRandomActorScheduler(7);
        await inconsistent.ScheduleAsync(new InconsistentControlledMailbox());
        await Assert.ThrowsAsync<InvalidOperationException>(() => inconsistent.StepAsync());
    }

    [Fact]
    public async Task Run_to_idle_waits_for_in_flight_turns_and_priority_scheduler_replays_seed()
    {
        var priority = new PriorityActorScheduler(2, priorityChangeBound: 1);
        await using var runtime = CreateTestingRuntime(priority);
        var slow = runtime.InvokeAsync("Testing", ActorId.Create("slow"), "Slow");
        var other = runtime.InvokeAsync("Testing", ActorId.Create("other"), "Add", "1");

        await runtime.RunToIdle();

        Assert.NotNull(await slow);
        Assert.NotNull(await other);
        Assert.Equal(2, priority.ReplayFromSeed().Seed);
        Assert.Equal(1, ((PriorityActorScheduler)priority.ReplayFromSeed()).PriorityChangeBound);
        Assert.Contains(runtime.Transcript, entry => entry.OperationName == "Slow");
    }

    [Fact]
    public void Virtual_time_rejects_invalid_advances_and_unattached_use()
    {
        var time = new VirtualActorTimeProvider();
        var start = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(start, new VirtualActorTimeProvider(start).GetUtcNow());
        Assert.Throws<ArgumentOutOfRangeException>(() => time.Advance(TimeSpan.FromTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => time.Advance(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => time.ScheduleTimer("Testing", ActorId.Create("bad"), "Timer", TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public async Task State_snapshot_exposes_state_machine_placeholders()
    {
        _ = typeof(CalculatorActor);
        await using var runtime = new ActorTestRuntime(services =>
        {
            services.AddSingleton<CalculatorDependency>();
            services.AddDaprActors(_ => { });
        });
        var actor = runtime.CreateActor<ICalculatorActor>(ActorId.Create("snapshot"), "Calculator");

        Assert.Null(runtime.StateOf(actor).CurrentState<string>());
        Assert.Null(runtime.StateOf(actor).Data<string>());
    }

    [Fact]
    public void Structural_analysis_defect_assertion_reports_defects()
    {
        var analysis = new ActorStateMachineAnalysis(typeof(TestingActor), [], ["defect"]);

        var exception = Assert.Throws<InvalidOperationException>(analysis.AssertNoStructuralDefects);
        Assert.Contains("defect", exception.Message);
    }

    private static ActorTestRuntime CreateTestingRuntime(ControlledActorScheduler scheduler) =>
        new(services => services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                "Testing",
                typeof(ITestingActor),
                typeof(TestingActor),
                (sp, _) => new TestingActor(
                    sp.GetRequiredService<ActorActivationContext>(),
                    sp.GetRequiredService<Dapr.Actors.Next.Core.Client.IActorInvocationClient>()),
                new TestingActorDispatcher(),
                new Dapr.Actors.Next.Core.Activation.ActorLifecycle(
                    (actor, cancellationToken) => ((Actor)actor).InvokeOnActivateAsync(cancellationToken),
                    (actor, cancellationToken) => ((Actor)actor).InvokeOnDeactivateAsync(cancellationToken),
                    (actor, context, cancellationToken) => ((Actor)actor).InvokeOnPreActorMethodAsync(context, cancellationToken),
                    (actor, context, exception, cancellationToken) => ((Actor)actor).InvokeOnPostActorMethodAsync(context, exception, cancellationToken)));
        }), new ActorTestRuntimeOptions { Scheduler = scheduler });

    private static async Task<(IReadOnlyList<InterleavingTranscriptEntry> Transcript, int Left, int Right)> RunTwoActorAdds(int seed)
    {
        await using var runtime = CreateTestingRuntime(new SeededRandomActorScheduler(seed));
        var left = runtime.InvokeAsync("Testing", ActorId.Create("left"), "Add", "1");
        var right = runtime.InvokeAsync("Testing", ActorId.Create("right"), "Add", "2");

        await runtime.RunToIdle();

        return (
            runtime.Transcript,
            int.Parse(System.Text.Encoding.UTF8.GetString((await left)!)),
            int.Parse(System.Text.Encoding.UTF8.GetString((await right)!)));
    }

    private sealed class DummyMailbox : Dapr.Actors.Next.Abstractions.Scheduling.IActorMailbox
    {
        public string ActorType => "Dummy";

        public ActorId ActorId => ActorId.Create("dummy");

        public ValueTask EnqueueAsync(Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn turn, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn?> TryDequeueAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn?>(null);
    }

    private sealed class InconsistentControlledMailbox : Dapr.Actors.Next.Core.Scheduling.IControlledActorMailbox
    {
        public string ActorType => "Inconsistent";

        public ActorId ActorId => ActorId.Create("inconsistent");

        public int PendingCount => 1;

        public bool IsExecuting => false;

        public Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn? Peek() => null;

        public Task<bool> ExecuteNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public ValueTask EnqueueAsync(Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn turn, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn?> TryDequeueAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Dapr.Actors.Next.Abstractions.Scheduling.ActorTurn?>(null);
    }
}
