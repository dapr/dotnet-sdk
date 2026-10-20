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
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Testing;
using Dapr.Common.Serialization;

namespace Dapr.Actors.Next.Core.Test;

public sealed class StateTransactionRegressionTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static ActorStateUnitOfWork Cache(IActorStateStore store, ActorFaults? faults = null) =>
        new("Actor", ActorId.Create("one"), store, new ActorWireSerializer(new JsonDaprSerializer()), faults);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Mutation_during_commit_uses_the_new_durable_baseline(int finalValue)
    {
        var store = new InMemoryActorStateStore();
        var faults = new ActorFaults();
        var cache = Cache(store, faults);
        var state = await cache.GetOrCreateAsync("value", () => 0);
        await cache.SaveStateAsync();
        state.Value = 1;
        using var pause = faults.PauseNextStateWrite<int>("value");
        var save = cache.SaveStateAsync().AsTask();
        await pause.Reached.WaitAsync(Deadline);
        state.Value = finalValue;
        pause.Release();
        await save.WaitAsync(Deadline);
        await cache.SaveStateAsync();
        await cache.EvictCacheAsync();
        Assert.Equal(finalValue, (await cache.TryGetAsync<int>("value"))!.Value);
        Assert.Equal(finalValue, (await Cache(store).TryGetAsync<int>("value"))!.Value);
    }

    [Fact]
    public async Task Eviction_waits_for_commit_without_blocking_reentrant_cache_access()
    {
        var store = new PausedStore();
        await store.WriteAsync("Actor", "one", "value", "1"u8.ToArray());
        var cache = Cache(store);
        await cache.SetAsync("value", 2);
        var save = cache.SaveStateAsync().AsTask();
        await store.Reached.Task.WaitAsync(Deadline);
        var eviction = cache.EvictCacheAsync(new DaprEvictStateOptions { EvictOnDirtyState = true }).AsTask();
        try
        {
            Assert.False(eviction.IsCompleted);
            Assert.Equal(2, (await cache.TryGetAsync<int>("value"))!.Value);
            await cache.SetAsync("other", 3);
        }
        finally
        {
            store.Release.TrySetResult();
        }
        await Task.WhenAll(save, eviction).WaitAsync(Deadline);
        Assert.Equal(2, (await cache.TryGetAsync<int>("value"))!.Value);
        Assert.Equal(2, (await Cache(store).TryGetAsync<int>("value"))!.Value);
        Assert.Null(await cache.TryGetAsync<int>("other"));
    }

    [Fact]
    public async Task Canceling_eviction_wait_does_not_release_another_commits_gate()
    {
        var store = new PausedStore();
        var cache = Cache(store);
        await cache.SetAsync("value", 2);
        var save = cache.SaveStateAsync().AsTask();
        await store.Reached.Task.WaitAsync(Deadline);
        using var cancellation = new CancellationTokenSource();
        var eviction = cache.EvictCacheAsync(new DaprEvictStateOptions { EvictOnDirtyState = true }, cancellation.Token).AsTask();
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => eviction);
            Assert.Equal(2, (await cache.TryGetAsync<int>("value"))!.Value);
        }
        finally
        {
            store.Release.TrySetResult();
        }
        await save.WaitAsync(Deadline);
        await cache.EvictCacheAsync();
        Assert.Equal(2, (await cache.TryGetAsync<int>("value"))!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replacement_during_commit_remains_dirty_and_next_save_wins(bool deleting)
    {
        var store = new PausedStore();
        var cache = Cache(store);
        if (deleting) await cache.RemoveAsync("value");
        else await cache.SetAsync("value", 1);
        var save = cache.SaveStateAsync().AsTask();
        await store.Reached.Task.WaitAsync(Deadline);
        await cache.SetAsync("value", 2);
        var secondSave = cache.SaveStateAsync().AsTask();
        store.Release.TrySetResult();
        await Task.WhenAll(save, secondSave).WaitAsync(Deadline);
        Assert.Equal(2, (await Cache(store).TryGetAsync<int>("value"))!.Value);
    }

    [Fact]
    public async Task Slow_cache_read_does_not_overwrite_a_newer_set()
    {
        var store = new PausedStore { PauseRead = true };
        var cache = Cache(store);
        var read = cache.TryGetAsync<int>("value").AsTask();
        await store.Reached.Task.WaitAsync(Deadline);
        await cache.SetAsync("value", 2);
        store.Release.TrySetResult();
        Assert.Equal(2, (await read.WaitAsync(Deadline))!.Value);
        await cache.FlushAsync();
        Assert.Equal(2, (await Cache(store).TryGetAsync<int>("value"))!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_snapshot_does_not_acknowledge_later_mutation_or_removal(bool removing)
    {
        var store = new PausedStore();
        var cache = Cache(store);
        var state = await cache.GetOrCreateAsync("value", () => 1);
        var save = cache.SaveStateAsync().AsTask();
        await store.Reached.Task.WaitAsync(Deadline);
        if (removing) await cache.RemoveAsync("value");
        else state.Value = 2;
        store.Release.TrySetResult();
        await save.WaitAsync(Deadline);
        await cache.FlushAsync();
        var durable = await Cache(store).TryGetAsync<int>("value");
        if (removing) Assert.Null(durable);
        else Assert.Equal(2, durable!.Value);
    }

    [Fact]
    public async Task Slow_read_cannot_repopulate_an_evicted_snapshot()
    {
        var store = new PausedStore { PauseRead = true };
        await store.WriteAsync("Actor", "one", "value", "1"u8.ToArray());
        var cache = Cache(store);
        var read = cache.TryGetAsync<int>("value").AsTask();
        await store.Reached.Task.WaitAsync(Deadline);
        await cache.EvictCacheAsync();
        await store.WriteAsync("Actor", "one", "value", "2"u8.ToArray());
        store.Release.TrySetResult();
        Assert.Equal(2, (await read.WaitAsync(Deadline))!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_batch_preserves_all_keys_and_retry_commits_all(bool transactionFault)
    {
        var store = new InMemoryActorStateStore();
        var faults = new ActorFaults();
        var cache = Cache(store, faults);
        await cache.SetAsync("first", 1);
        await cache.SetAsync("second", 2L);
        await cache.SetAsync("deleted", 3);
        await cache.FlushAsync();
        await cache.SetAsync("first", 10);
        await cache.SetAsync("second", 20L);
        await cache.RemoveAsync("deleted");
        if (transactionFault) faults.FailNextStateTransaction();
        else faults.FailNextStateWrite<long>();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => cache.FlushAsync().AsTask());
        var durable = Cache(store);
        Assert.Equal(1, (await durable.TryGetAsync<int>("first"))!.Value);
        Assert.Equal(2L, (await durable.TryGetAsync<long>("second"))!.Value);
        Assert.Equal(3, (await durable.TryGetAsync<int>("deleted"))!.Value);
        await cache.FlushAsync();
        durable = Cache(store);
        Assert.Equal(10, (await durable.TryGetAsync<int>("first"))!.Value);
        Assert.Equal(20L, (await durable.TryGetAsync<long>("second"))!.Value);
        Assert.Null(await durable.TryGetAsync<int>("deleted"));
    }

    [Fact]
    public async Task Delete_only_transaction_can_fail_and_retry()
    {
        var store = new InMemoryActorStateStore();
        var faults = new ActorFaults();
        var cache = Cache(store, faults);
        await cache.SetAsync("value", 1);
        await cache.FlushAsync();
        await cache.RemoveAsync("value");
        faults.FailNextStateTransaction();
        await Assert.ThrowsAsync<ActorInjectedTransientException>(() => cache.FlushAsync().AsTask());
        Assert.Equal(1, (await Cache(store).TryGetAsync<int>("value"))!.Value);
        await cache.FlushAsync();
        Assert.Null(await Cache(store).TryGetAsync<int>("value"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_store_commit_retains_entire_batch_for_retry(bool canceled)
    {
        var store = new PausedStore();
        store.Release.SetResult();
        var cache = Cache(store);
        await cache.SetAsync("updated", 1);
        await cache.SetAsync("deleted", 1);
        await cache.SaveStateAsync();
        await cache.SetAsync("added", 2);
        await cache.SetAsync("updated", 2);
        await cache.RemoveAsync("deleted");
        store.CommitFailure = canceled
            ? new OperationCanceledException(new CancellationToken(true))
            : new InvalidOperationException("State store unavailable.");

        if (canceled)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.SaveStateAsync().AsTask());
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SaveStateAsync().AsTask());

        var durable = Cache(store);
        Assert.Null(await durable.TryGetAsync<int>("added"));
        Assert.Equal(1, (await durable.TryGetAsync<int>("updated"))!.Value);
        Assert.Equal(1, (await durable.TryGetAsync<int>("deleted"))!.Value);
        Assert.Null(await cache.TryGetAsync<int>("deleted"));
        await cache.SaveStateAsync();
        durable = Cache(store);
        Assert.Equal(2, (await durable.TryGetAsync<int>("added"))!.Value);
        Assert.Equal(2, (await durable.TryGetAsync<int>("updated"))!.Value);
        Assert.Null(await durable.TryGetAsync<int>("deleted"));
        await cache.EvictCacheAsync();
    }

    private sealed class PausedStore : IActorStateStore
    {
        private readonly InMemoryActorStateStore inner = new();
        public bool PauseRead { get; init; }
        public Exception? CommitFailure { get; set; }
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask ApplyAsync(string actorType, string actorId, IReadOnlyList<ActorStateOperation> operations, CancellationToken cancellationToken = default)
        {
            if (!PauseRead)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            if (CommitFailure is { } failure)
            {
                CommitFailure = null;
                throw failure;
            }
            await inner.ApplyAsync(actorType, actorId, operations, cancellationToken);
        }
        public async ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
        {
            var value = await inner.ReadAsync(actorType, actorId, name, cancellationToken);
            if (PauseRead)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return value;
        }
        public ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(actorType, actorId, name, value, cancellationToken);
        public ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(actorType, actorId, name, cancellationToken);
    }
}
