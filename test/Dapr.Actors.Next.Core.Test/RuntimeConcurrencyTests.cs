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
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.DependencyInjection;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

public sealed class RuntimeConcurrencyTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("Dapr-Reentrancy-Id")]
    [InlineData("dapr-reentrancy-id")]
    [InlineData("DAPR-REENTRANCY-ID")]
    [InlineData("dapr-reentrant-id")]
    [InlineData("x-dapr-reentrant-id")]
    public void Headers_normalize_canonical_and_legacy_names_without_mutating_input(string name)
    {
        var input = new Dictionary<string, string> { [name] = "chain", ["custom"] = "metadata" };
        var actual = ActorHeaders.WithCurrentReentrancy(input);
        Assert.Equal("chain", actual["Dapr-Reentrancy-Id"]);
        Assert.Equal("metadata", actual["custom"]);
        Assert.Equal(2, input.Count);
    }

    [Fact]
    public void Canonical_header_takes_precedence_over_legacy_aliases()
    {
        var headers = new Dictionary<string, string>
        {
            ["x-dapr-reentrant-id"] = "old",
            ["dapr-reentrant-id"] = "legacy",
            ["dapr-reentrancy-id"] = "canonical",
        };
        Assert.Equal("canonical", ActorHeaders.WithCurrentReentrancy(headers)["Dapr-Reentrancy-Id"]);
    }

    [Fact]
    public async Task Captured_execution_context_does_not_reuse_a_completed_turns_call_chain()
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<IReadOnlyDictionary<string, string>>? captured = null;
        probe.Handle = (_, _, _) =>
        {
            captured = Task.Run(async () =>
            {
                await release.Task;
                return ActorHeaders.WithCurrentReentrancy(ActorHeaders.Empty);
            });
            return Task.CompletedTask;
        };
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Capture");
        await runtime.RunToIdle().WaitAsync(Deadline);
        await call;
        release.SetResult();
        Assert.Empty(await captured!.WaitAsync(Deadline));
    }

    [Fact]
    public async Task Root_turn_waits_for_admitted_child_before_deactivation()
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe, remote: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<byte[]?>? child = null;
        probe.Handle = async (actor, method, ct) =>
        {
            if (method == "Start")
            {
                child = actor.Call("a", "Child", ct);
                await entered.Task.WaitAsync(ct);
                returned.TrySetResult();
            }
            else
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                await actor.Context.State.SetAsync("count", 42, ct);
            }
        };
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Start");
        await runtime.StepAsync();
        await returned.Task.WaitAsync(Deadline);
        var deactivate = runtime.DeactivateAsync("Concurrency", ActorId.Create("a"));
        try
        {
            Assert.False(call.IsCompleted);
            Assert.False(await runtime.StepAsync());
            Assert.Equal(0, Assert.Single(probe.Actors).DisposeCount);
        }
        finally
        {
            release.TrySetResult();
        }

        await runtime.RunToIdle().WaitAsync(Deadline);
        await Task.WhenAll(call, child!, deactivate);
        Assert.Equal(42, runtime.StateOf("Concurrency", ActorId.Create("a")).Get<int>("count"));
        Assert.Equal(1, Assert.Single(probe.Actors).DisposeCount);
    }

    [Fact]
    public async Task Completed_turn_stops_reentrant_admission_before_flushing_state()
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe);
        using var pause = runtime.Faults.PauseNextStateWrite<int>("count");
        probe.Handle = (actor, method, ct) =>
            actor.Context.State.SetAsync("count", method == "First" ? 1 : 2, ct).AsTask();
        var headers = new Dictionary<string, string> { ["Dapr-Reentrancy-Id"] = "shared-chain" };
        var first = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "First", headers: headers);
        await runtime.StepAsync();
        await pause.Reached.WaitAsync(Deadline);
        var next = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Next", headers: headers);
        try
        {
            Assert.False(next.IsCompleted);
            Assert.False(await runtime.StepAsync());
        }
        finally
        {
            pause.Release();
        }

        await runtime.RunToIdle().WaitAsync(Deadline);
        await Task.WhenAll(first, next);
        Assert.Equal(2, runtime.StateOf("Concurrency", ActorId.Create("a")).Get<int>("count"));
    }

    [Fact]
    public async Task Deactivation_from_captured_context_queues_after_turn_completion_begins()
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe);
        using var pause = runtime.Faults.PauseNextStateWrite<int>("count");
        var request = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var issued = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? background = null;
        probe.Handle = async (actor, method, ct) =>
        {
            if (method != "Write")
            {
                return;
            }

            await actor.Context.State.SetAsync("count", 1, ct);
            background = Task.Run(async () =>
            {
                await request.Task;
                var deactivation = actor.Runtime.DeactivateAsync("Concurrency", actor.Context.ActorId);
                issued.TrySetResult(deactivation);
                await deactivation;
            });
        };
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Write");
        await runtime.StepAsync();
        await pause.Reached.WaitAsync(Deadline);
        request.TrySetResult();
        var deactivate = await issued.Task.WaitAsync(Deadline);
        try
        {
            Assert.False(deactivate.IsCompleted);
        }
        finally
        {
            pause.Release();
        }

        await runtime.RunToIdle().WaitAsync(Deadline);
        await Task.WhenAll(call, background!);
        Assert.Equal(1, Assert.Single(probe.Actors).DisposeCount);

        var next = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Read");
        await runtime.RunToIdle().WaitAsync(Deadline);
        await next;
        Assert.Equal(2, probe.Actors.Count);
        Assert.Equal(1, probe.Actors.Sum(actor => actor.DisposeCount));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Reentrant_calls_cross_actor_and_execution_context_boundaries(bool anotherActor, bool remote)
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe, remote);
        probe.Handle = async (actor, method, ct) =>
        {
            if (method == "Start")
            {
                await actor.Call(anotherActor ? "b" : "a", anotherActor ? "Relay" : "Leaf", ct);
            }
            else if (method == "Relay")
            {
                await actor.Call("a", "Leaf", ct);
            }
            else if (method == "Leaf")
            {
                var state = await actor.Context.State.GetOrCreateAsync("count", () => 0, ct);
                state.Value++;
            }
        };

        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Start");
        using var timeout = new CancellationTokenSource(Deadline);
        await runtime.RunToIdle(timeout.Token);
        await call.WaitAsync(timeout.Token);
        Assert.Equal(1, runtime.StateOf("Concurrency", ActorId.Create("a")).Get<int>("count"));
    }

    [Fact]
    public async Task One_shot_remote_boundary_fault_is_consumed_only_by_matching_nested_invocation()
    {
        var ambient = new AsyncLocal<string?>();
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe);
        runtime.Faults.SimulateNextRemoteInvocation("Concurrency", "Remote");
        var contexts = new ConcurrentDictionary<string, string>();
        probe.Handle = async (actor, method, ct) =>
        {
            if (method == "Start")
            {
                ambient.Value = "caller";
                await actor.Call("a", "Local", ct);
                await actor.Call("a", "Remote", ct);
                await actor.Call("a", "Again", ct);
            }
            else
            {
                contexts[method] = ambient.Value ?? "detached";
            }
        };

        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Start");
        using var timeout = new CancellationTokenSource(Deadline);
        await runtime.RunToIdle(timeout.Token);
        await call;
        Assert.Equal("caller", contexts["Local"]);
        Assert.Equal("detached", contexts["Remote"]);
        Assert.Equal("caller", contexts["Again"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deactivation_waits_for_state_commit_and_disposes_once(bool selfDeactivate)
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe);
        using var pause = runtime.Faults.PauseNextStateWrite<int>("count");
        probe.Handle = async (actor, _, ct) =>
        {
            await actor.Context.State.SetAsync("count", 42, ct);
            if (selfDeactivate)
            {
                await actor.Runtime.DeactivateAsync("Concurrency", actor.Context.ActorId, ct);
                Assert.Equal(0, actor.DisposeCount);
            }
        };
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Write");
        Assert.True(await runtime.StepAsync());
        await pause.Reached.WaitAsync(Deadline);
        var activation = Assert.Single(probe.Actors);
        var deactivation = selfDeactivate ? Task.CompletedTask : runtime.DeactivateAsync("Concurrency", ActorId.Create("a"));
        try
        {
            Assert.Equal(0, activation.DisposeCount);
            Assert.False(await runtime.StepAsync());
            Assert.Equal(0, runtime.StateOf("Concurrency", ActorId.Create("a")).Get<int>("count"));
        }
        finally
        {
            pause.Release();
        }

        using var timeout = new CancellationTokenSource(Deadline);
        await runtime.RunToIdle(timeout.Token);
        await Task.WhenAll(call, deactivation).WaitAsync(timeout.Token);
        Assert.Equal(42, runtime.StateOf("Concurrency", ActorId.Create("a")).Get<int>("count"));
        Assert.Equal(1, activation.DisposeCount);
        Assert.Equal(1, probe.Deactivations);
    }

    [Fact]
    public async Task Deactivation_disposes_actor_even_when_lifecycle_callback_fails()
    {
        var probe = new Probe { DeactivateError = new InvalidOperationException("deactivation failed") };
        await using var runtime = CreateRuntime(probe);
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Read");
        await runtime.RunToIdle();
        await call;
        var deactivate = runtime.DeactivateAsync("Concurrency", ActorId.Create("a"));
        await runtime.RunToIdle();
        Assert.Same(probe.DeactivateError, await Assert.ThrowsAsync<InvalidOperationException>(() => deactivate));
        Assert.Equal(1, Assert.Single(probe.Actors).DisposeCount);
    }

    [Theory]
    [InlineData(false, 32, 1, false)]
    [InlineData(true, 3, 3, false)]
    [InlineData(false, 32, 1, true)]
    [InlineData(true, 3, 3, true)]
    public async Task Explicit_reentrancy_options_are_enforced_and_mailbox_recovers(bool enabled, int maxDepth, int expectedCalls, bool typeOverride)
    {
        var probe = new Probe
        {
            EnableReentrancy = typeOverride ? !enabled : enabled,
            MaxDepth = typeOverride ? 1 : maxDepth,
            TypeOptions = typeOverride ? new DaprActorTypeOptions { EnableReentrancy = enabled, MaxReentrantDepth = maxDepth } : null,
        };
        await using var runtime = CreateRuntime(probe, remote: true);
        var calls = 0;
        probe.Handle = async (actor, method, ct) =>
        {
            if (method == "Recurse")
            {
                Interlocked.Increment(ref calls);
                await actor.Call("a", "Recurse", ct);
            }
        };

        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Recurse");
        using var timeout = new CancellationTokenSource(Deadline);
        await runtime.RunToIdle(timeout.Token);
        await Assert.ThrowsAsync<ActorInvocationException>(() => call);
        Assert.Equal(expectedCalls, calls);
        var next = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Read");
        await runtime.RunToIdle(timeout.Token);
        await next;
    }

    [Fact]
    public async Task Failed_flush_allows_reentrant_calls_from_error_lifecycle_hook()
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe, remote: true);
        runtime.Faults.FailNextStateWrite<int>(stateName: "count");
        var recoveries = 0;
        probe.Handle = async (actor, method, ct) =>
        {
            if (method == "Write")
            {
                await actor.Context.State.SetAsync("count", 1, ct);
            }
            else
            {
                Interlocked.Increment(ref recoveries);
            }
        };
        probe.Post = async (actor, error, ct) =>
        {
            if (error is not null)
            {
                await actor.Call("a", "Recover", ct);
            }
        };
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Write");
        await runtime.RunToIdle().WaitAsync(Deadline);
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => call);
        Assert.Equal(1, recoveries);
        Assert.Equal(1, runtime.StateOf("Concurrency", ActorId.Create("a")).Get<int>("count"));
    }

    [Fact]
    public async Task State_write_pause_matches_once_and_honors_write_cancellation()
    {
        var faults = new ActorFaults();
        using var pause = faults.PauseNextStateWrite<int>("selected");
        await faults.BeforeWriteAsync(typeof(string), "Actor", "a", "selected");
        await faults.BeforeWriteAsync(typeof(int), "Actor", "a", "other");
        Assert.False(pause.Reached.IsCompleted);
        using var stop = new CancellationTokenSource();
        var write = faults.BeforeWriteAsync(typeof(int), "Actor", "a", "selected", stop.Token).AsTask();
        await pause.Reached.WaitAsync(Deadline);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        await faults.BeforeWriteAsync(typeof(int), "Actor", "a", "selected").AsTask().WaitAsync(Deadline);
    }

    [Fact]
    public async Task Controlled_scheduler_can_be_cancelled_while_a_turn_is_paused()
    {
        var probe = new Probe();
        await using var runtime = CreateRuntime(probe);
        using var pause = runtime.Faults.PauseNextStateWrite<int>();
        probe.Handle = (actor, _, ct) => actor.Context.State.SetAsync("count", 1, ct).AsTask();
        var call = runtime.InvokeAsync("Concurrency", ActorId.Create("a"), "Write");
        await runtime.StepAsync();
        await pause.Reached.WaitAsync(Deadline);
        using var stop = new CancellationTokenSource();
        var running = runtime.RunToIdle(stop.Token);
        stop.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(Deadline));
        }
        finally
        {
            pause.Release();
        }

        await runtime.RunToIdle().WaitAsync(Deadline);
        await call;
    }

    private static ActorTestRuntime CreateRuntime(Probe probe, bool remote = false) =>
        new(services =>
        {
            services.Configure<DaprActorsOptions>(options =>
            {
                options.EnableSidecarTransport = false;
                options.EnableReentrancy = probe.EnableReentrancy;
                options.MaxReentrantDepth = probe.MaxDepth;
            });
            services.AddDaprActorsCore(registrations => registrations.Add("Concurrency", typeof(IActor), typeof(TestActor),
                (sp, _) =>
                {
                    var actor = new TestActor(sp.GetRequiredService<ActorActivationContext>(),
                        sp.GetRequiredService<IActorInvocationClient>(), sp.GetRequiredService<IActorRuntime>());
                    probe.Actors.Add(actor);
                    return actor;
                }, new Dispatcher(probe), new ActorLifecycle(
                    static (_, _) => ValueTask.CompletedTask,
                    (_, _) =>
                    {
                        Interlocked.Increment(ref probe.Deactivations);
                        return probe.DeactivateError is null ? ValueTask.CompletedTask : ValueTask.FromException(probe.DeactivateError);
                    },
                    static (_, _, _) => ValueTask.CompletedTask,
                    (actor, _, error, ct) => probe.Post?.Invoke((TestActor)actor, error, ct) ?? ValueTask.CompletedTask),
                typeOptions: probe.TypeOptions));
        }, new ActorTestRuntimeOptions { SimulateRemoteCalls = remote });

    private sealed class Probe
    {
        public ConcurrentBag<TestActor> Actors { get; } = new();
        public Func<TestActor, string, CancellationToken, Task> Handle { get; set; } = static (_, _, _) => Task.CompletedTask;
        public bool EnableReentrancy { get; init; } = true;
        public int MaxDepth { get; init; } = 32;
        public DaprActorTypeOptions? TypeOptions { get; init; }
        public int Deactivations;
        public Exception? DeactivateError { get; init; }
        public Func<TestActor, Exception?, CancellationToken, ValueTask>? Post { get; set; }
    }

    private sealed class TestActor(ActorActivationContext context, IActorInvocationClient invocations, IActorRuntime runtime) : IActor, IDisposable
    {
        public ActorActivationContext Context { get; } = context;
        public IActorRuntime Runtime { get; } = runtime;
        public int DisposeCount;
        public Task<byte[]?> Call(string id, string method, CancellationToken ct) =>
            invocations.InvokeAsync("Concurrency", id, method, ReadOnlyMemory<byte>.Empty, ActorHeaders.Empty, ct);
        public void Dispose() => Interlocked.Increment(ref DisposeCount);
    }

    private sealed class Dispatcher(Probe probe) : IActorDispatcher
    {
        public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
        {
            await probe.Handle((TestActor)actor, request.MethodName, cancellationToken);
            return new ActorDispatchResponse(null);
        }
    }
}
