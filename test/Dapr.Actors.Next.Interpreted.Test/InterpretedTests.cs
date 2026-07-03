using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Interpreted;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Interpreted.Test;

public sealed class InterpretedTests
{
    private static readonly ActorId Id = ActorId.Create("machine-1");

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Table_execution_matches_compiled_equivalent()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, registry, Definition());
        await using var runtime = Runtime(store, registry);

        var first = await Raise(runtime, new("bid", Json(7)));
        var second = await Raise(runtime, new("close", Json<object?>(null)));

        Assert.Equal("Open", first.RootElement.GetProperty("State").GetString());
        Assert.Equal("Closed", second.RootElement.GetProperty("State").GetString());
        Assert.Equal(CompiledEquivalent([7], close: true), second.RootElement.GetProperty("State").GetString());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Verifier_rejects_unknown_capability_names()
    {
        var result = new InterpretedMachineVerifier(new TestCapabilityRegistry()).Verify(
            Definition(effectName: "missing-effect"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Defects, item => item.Contains("missing-effect", StringComparison.Ordinal));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Verifier_reports_structural_and_behavioral_defects()
    {
        var verifier = new InterpretedMachineVerifier(new TestCapabilityRegistry());
        var result = verifier.Verify(new InterpretedMachineDefinition
        {
            DocumentVersion = 0,
            InitialState = "Missing",
            States =
            [
                new InterpretedStateDefinition { Name = "Open" },
                new InterpretedStateDefinition { Name = "Open" },
                new InterpretedStateDefinition { Name = "Unreachable" },
            ],
            Transitions =
            [
                new InterpretedTransitionDefinition
                {
                    Source = "Unknown",
                    Event = "",
                    Branches =
                    [
                        new InterpretedBranchDefinition
                        {
                            Target = "AlsoMissing",
                            Effects = ["increment"],
                        },
                    ],
                },
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "go",
                    Branches = [],
                },
            ],
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Defects, item => item.Contains("DocumentVersion", StringComparison.Ordinal));
        Assert.Contains(result.Defects, item => item.Contains("InitialState", StringComparison.Ordinal));
        Assert.Contains(result.Defects, item => item.Contains("duplicated", StringComparison.Ordinal));
        Assert.Contains(result.Defects, item => item.Contains("dead end", StringComparison.Ordinal));
        Assert.Contains(result.Defects, item => item.Contains("no branches", StringComparison.Ordinal));

        var unreachable = verifier.Verify(new InterpretedMachineDefinition
        {
            InitialState = "Open",
            States =
            [
                new InterpretedStateDefinition { Name = "Open", Terminal = true },
                new InterpretedStateDefinition { Name = "Unreachable", Terminal = true },
            ],
        });
        Assert.Contains(unreachable.Defects, item => item.Contains("unreachable", StringComparison.Ordinal));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Verify_before_activate_rejects_defective_machine()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await store.SetAsync("Machine", Id, Definition(includeClosedState: false));
        await using var runtime = Runtime(store, registry);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Raise(runtime, new("bid", Json(1))));
        Assert.Contains("verification failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Dynamic_state_round_trips_after_deactivation()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, registry, Definition());
        await using var runtime = Runtime(store, registry);

        await Raise(runtime, new("bid", Json(3)));
        var deactivate = runtime.InvokeAsync("Machine", Id, "deactivate", kind: Dapr.Actors.Next.Abstractions.Scheduling.ActorTurnKind.Deactivate);
        await runtime.RunToIdle();
        await deactivate;
        var result = await Raise(runtime, new("bid", Json(4)));

        var count = result.RootElement
            .GetProperty("Data")
            .GetProperty("Values")
            .GetProperty("count")
            .GetInt32();
        Assert.Equal(2, count);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Guard_fallthrough_returns_rejected_without_running_effect()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, registry, Definition());
        await using var runtime = Runtime(store, registry);

        var result = await Raise(runtime, new("bid", Json(-1)));

        Assert.Equal("Open", result.RootElement.GetProperty("State").GetString());
        Assert.Equal("rejected", result.RootElement.GetProperty("Reply").GetString());
        Assert.False(result.RootElement.GetProperty("Data").GetProperty("Values").TryGetProperty("count", out _));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Missing_transition_and_no_matching_branch_are_rejected()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, registry, Definition());
        await using var runtime = Runtime(store, registry);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => Raise(runtime, new("unknown", Json(1))));
        Assert.Contains("does not handle", missing.Message, StringComparison.Ordinal);

        var strictStore = new InMemoryInterpretedMachineStore();
        await Deploy(strictStore, registry, StrictGuardDefinition());
        await using var strictRuntime = Runtime(strictStore, registry);
        var noBranch = await Assert.ThrowsAsync<InvalidOperationException>(() => Raise(strictRuntime, new("bid", Json(-1))));
        Assert.Contains("No branch matched", noBranch.Message, StringComparison.Ordinal);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Runtime_rejects_capabilities_removed_after_verification()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, registry, MissingGuardAtRuntimeDefinition());
        await using var guardRuntime = Runtime(store, registry);

        var missingGuard = await Assert.ThrowsAsync<InvalidOperationException>(() => Raise(guardRuntime, new("go", Json(1))));
        Assert.Contains("Guard 'positive'", missingGuard.Message, StringComparison.Ordinal);

        var effectRegistry = new TestCapabilityRegistry();
        var effectStore = new InMemoryInterpretedMachineStore();
        await Deploy(effectStore, effectRegistry, MissingEffectAtRuntimeDefinition());
        await using var effectRuntime = Runtime(effectStore, effectRegistry);

        var missingEffect = await Assert.ThrowsAsync<InvalidOperationException>(() => Raise(effectRuntime, new("go", Json(1))));
        Assert.Contains("Effect 'second'", missingEffect.Message, StringComparison.Ordinal);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Deployer_applies_generate_verify_deploy_guard()
    {
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        var deployer = new InterpretedMachineDeployer(new InterpretedMachineVerifier(registry), store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => deployer.DeployAsync("Machine", Id, Definition(effectName: "unknown")).AsTask());
        await deployer.DeployAsync("Machine", Id, Definition());

        Assert.NotNull(await store.GetAsync("Machine", Id));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public async Task Dispatcher_rejects_unknown_operation_and_bad_payload()
    {
        var dispatcher = new InterpretedStateMachineDispatcher();
        var registry = new TestCapabilityRegistry();
        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, registry, Definition());
        await using var runtime = Runtime(store, registry);
        var actor = (InterpretedStateMachineActor)RuntimeHelpers.GetUninitializedObject(typeof(InterpretedStateMachineActor));

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(actor, new Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchRequest("Machine", Id, "Other", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), new ActorRequestContext(null, null, new Dictionary<string, string>())), default).AsTask());
        await Assert.ThrowsAsync<JsonException>(() => dispatcher.DispatchAsync(actor, new Dapr.Actors.Next.Abstractions.Dispatching.ActorDispatchRequest("Machine", Id, "Raise", System.Text.Encoding.UTF8.GetBytes("{"), new Dictionary<string, string>(), new ActorRequestContext(null, null, new Dictionary<string, string>())), default).AsTask());
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Dynamic_state_bag_returns_default_for_missing_values_and_default_registry_rejects_all()
    {
        var bag = new DynamicStateBag();
        Assert.Equal(0, bag.Get<int>("missing"));

        var services = new ServiceCollection();
        services.AddDaprInterpretedActors("Machine");
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ICapabilityRegistry>();

        Assert.False(registry.TryGetEffect("x", out _));
        Assert.False(registry.TryGetGuard("x", out _));
    }

    private static ActorTestRuntime Runtime(IInterpretedMachineStore store, ICapabilityRegistry registry) =>
        new(services =>
        {
            services.AddSingleton(store);
            services.AddSingleton(registry);
            services.AddDaprInterpretedActors("Machine");
        });

    private static async Task<JsonDocument> Raise(ActorTestRuntime runtime, InterpretedEvent evt)
    {
        var json = JsonSerializer.Serialize(evt);
        var pending = runtime.InvokeAsync("Machine", Id, "Raise", json);
        await runtime.RunToIdle();
        var bytes = await pending;
        return JsonDocument.Parse(Encoding.UTF8.GetString(bytes!));
    }

    private static async Task Deploy(InMemoryInterpretedMachineStore store, ICapabilityRegistry registry, InterpretedMachineDefinition definition)
    {
        var deployer = new InterpretedMachineDeployer(new InterpretedMachineVerifier(registry), store);
        await deployer.DeployAsync("Machine", Id, definition);
    }

    private static InterpretedMachineDefinition Definition(
        string effectName = "increment",
        bool includeClosedState = true)
    {
        var states = includeClosedState
            ? new[]
            {
                new InterpretedStateDefinition { Name = "Open" },
                new InterpretedStateDefinition { Name = "Closed", Terminal = true },
            }
            : [new InterpretedStateDefinition { Name = "Open" }];

        return new InterpretedMachineDefinition
        {
            DocumentVersion = 1,
            InitialState = "Open",
            States = states,
            Transitions =
            [
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "bid",
                    Branches =
                    [
                        new InterpretedBranchDefinition
                        {
                            Guards = ["positive"],
                            Effects = [effectName],
                            Reply = Json("accepted"),
                        },
                        new InterpretedBranchDefinition
                        {
                            Otherwise = true,
                            Reply = Json("rejected"),
                        },
                    ],
                },
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "close",
                    Branches =
                    [
                        new InterpretedBranchDefinition
                        {
                            Otherwise = true,
                            Target = "Closed",
                            Reply = Json("closed"),
                        },
                    ],
                },
            ],
        };
    }

    private static InterpretedMachineDefinition StrictGuardDefinition() =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Open",
            States = [new InterpretedStateDefinition { Name = "Open" }],
            Transitions =
            [
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "bid",
                    Branches =
                    [
                        new InterpretedBranchDefinition
                        {
                            Guards = ["positive"],
                            Effects = ["increment"],
                        },
                    ],
                },
            ],
        };

    private static InterpretedMachineDefinition MissingGuardAtRuntimeDefinition() =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Open",
            States = [new InterpretedStateDefinition { Name = "Open" }],
            Transitions =
            [
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "go",
                    Branches =
                    [
                        new InterpretedBranchDefinition { Guards = ["remove-positive"] },
                        new InterpretedBranchDefinition { Guards = ["positive"] },
                    ],
                },
            ],
        };

    private static InterpretedMachineDefinition MissingEffectAtRuntimeDefinition() =>
        new()
        {
            DocumentVersion = 1,
            InitialState = "Open",
            States = [new InterpretedStateDefinition { Name = "Open" }],
            Transitions =
            [
                new InterpretedTransitionDefinition
                {
                    Source = "Open",
                    Event = "go",
                    Branches =
                    [
                        new InterpretedBranchDefinition
                        {
                            Otherwise = true,
                            Effects = ["remove-second", "second"],
                        },
                    ],
                },
            ],
        };

    private static JsonElement Json<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static string CompiledEquivalent(IReadOnlyList<int> bids, bool close)
    {
        var state = "Open";
        foreach (var bid in bids)
        {
            if (state == "Open" && bid > 0)
            {
                continue;
            }
        }

        return close && state == "Open" ? "Closed" : state;
    }

    private sealed class TestCapabilityRegistry : ICapabilityRegistry
    {
        private readonly Dictionary<string, IActorEffect> effects = new(StringComparer.Ordinal)
        {
            ["increment"] = new IncrementEffect(),
            ["second"] = new NoopEffect(),
        };

        private readonly Dictionary<string, IActorGuard> guards;

        public TestCapabilityRegistry()
        {
            effects["remove-second"] = new RemoveEffect(this, "second");
            guards = new Dictionary<string, IActorGuard>(StringComparer.Ordinal)
            {
                ["positive"] = new PositiveGuard(),
                ["remove-positive"] = new RemoveGuard(this, "positive"),
            };
        }

        public bool TryGetEffect(string name, out IActorEffect effect) => effects.TryGetValue(name, out effect!);

        public bool TryGetGuard(string name, out IActorGuard guard) => guards.TryGetValue(name, out guard!);

        public void RemoveEffect(string name) => effects.Remove(name);

        public void RemoveGuard(string name) => guards.Remove(name);
    }

    private sealed class IncrementEffect : IActorEffect
    {
        public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
        {
            var state = (DynamicStateBag)context.Arguments["state"]!;
            state.Set("count", state.Get<int>("count") + 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopEffect : IActorEffect
    {
        public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class RemoveEffect(TestCapabilityRegistry registry, string name) : IActorEffect
    {
        public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
        {
            registry.RemoveEffect(name);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PositiveGuard : IActorGuard
    {
        public ValueTask<bool> EvaluateAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
        {
            var payload = (JsonElement)context.Arguments["payload"]!;
            return ValueTask.FromResult(payload.GetInt32() > 0);
        }
    }

    private sealed class RemoveGuard(TestCapabilityRegistry registry, string name) : IActorGuard
    {
        public ValueTask<bool> EvaluateAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
        {
            registry.RemoveGuard(name);
            return ValueTask.FromResult(false);
        }
    }
}
