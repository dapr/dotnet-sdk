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
//  ------------------------------------------------------------------------

using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Worker.Grpc;

namespace Dapr.Workflow.Test.Worker.Grpc;

/// <summary>
/// Unit tests for the worker's stateful-history cache bounds. These mirror the Go reference
/// (durabletask-go client/worker_history_test.go) and the Python SDK: a sliding TTL, an
/// instance-count cap, and a byte budget, all with least-recently-used eviction.
/// </summary>
public sealed class WorkflowHistoryCacheTests
{
    /// <summary>A controllable clock for deterministic TTL/LRU tests.</summary>
    private sealed class TestClock
    {
        public DateTime Now { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTime Read() => Now;

        public void Advance(TimeSpan delta) => Now += delta;
    }

    /// <summary>Events with non-zero serialized size (EventId 0 is the proto default, which is 0 bytes).</summary>
    private static List<HistoryEvent> Events(int count)
    {
        var events = new List<HistoryEvent>(count);
        for (var i = 0; i < count; i++)
        {
            events.Add(new HistoryEvent { EventId = i + 1 });
        }

        return events;
    }

    private static long BytesOf(int count) => Events(count).Sum(e => (long)e.CalculateSize());

    [Fact]
    public void WriteFromARetiredStreamIsIgnored()
    {
        // A work item can outlive the stream it arrived on: ReceiveLoopAsync abandons in-flight
        // tasks on cancellation, so a handler that ignores the token can finish after Reset. Such a
        // late write must not repopulate the cache the next stream is already using, or a matching
        // event count could accept history carried over from the previous stream.
        var cache = new WorkflowHistoryCache();
        var retiredGeneration = cache.Generation;

        cache.Reset();

        cache.Put("a", Events(3), retiredGeneration);
        Assert.Null(cache.Get("a"));

        cache.Put("a", Events(3), cache.Generation);
        Assert.NotNull(cache.Get("a"));

        // Same rule for eviction: a retired stream must not drop an entry the current one relies on.
        cache.Remove("a", retiredGeneration);
        Assert.NotNull(cache.Get("a"));

        cache.Remove("a", cache.Generation);
        Assert.Null(cache.Get("a"));
    }

    [Fact]
    public void GetPutRemoveReset()
    {
        var cache = new WorkflowHistoryCache();

        Assert.Null(cache.Get("a"));

        cache.Put("a", Events(3), cache.Generation);
        var cached = cache.Get("a");
        Assert.NotNull(cached);
        Assert.Equal(3, cached!.Count);

        cache.Remove("a", cache.Generation);
        Assert.Null(cache.Get("a"));

        cache.Put("b", Events(1), cache.Generation);
        cache.Reset();
        Assert.Null(cache.Get("b"));
    }

    [Fact]
    public void CountCapEvictsLeastRecentlyUsed()
    {
        var clock = new TestClock();
        var cache = new WorkflowHistoryCache(maxInstances: 2, clock: clock.Read);

        cache.Put("a", Events(1), cache.Generation);
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.Put("b", Events(1), cache.Generation);
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.Put("c", Events(1), cache.Generation); // over the cap, evicts the LRU entry ("a")

        Assert.Null(cache.Get("a"));
        Assert.NotNull(cache.Get("b"));
        Assert.NotNull(cache.Get("c"));
    }

    [Fact]
    public void ByteCapEvictsLeastRecentlyUsed()
    {
        var entryBytes = BytesOf(4);
        Assert.True(entryBytes > 0);
        var clock = new TestClock();
        var cache = new WorkflowHistoryCache(maxBytes: entryBytes + 1, clock: clock.Read);

        cache.Put("a", Events(4), cache.Generation);
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.Put("b", Events(4), cache.Generation); // two entries exceed the byte budget, evicts the LRU entry ("a")

        Assert.Null(cache.Get("a"));
        Assert.NotNull(cache.Get("b"));
        Assert.True(cache.TotalBytes <= entryBytes + 1);
    }

    [Fact]
    public void SingleOversizedEntryIsKept()
    {
        var cache = new WorkflowHistoryCache(maxBytes: 1);
        cache.Put("big", Events(5), cache.Generation);
        Assert.NotNull(cache.Get("big"));
    }

    [Fact]
    public void ByteAccountingTracksReplaceAndRemove()
    {
        // A budget large enough that nothing is evicted; accounting only runs when one is configured.
        var cache = new WorkflowHistoryCache(maxBytes: 1024 * 1024);

        cache.Put("a", Events(3), cache.Generation);
        cache.Put("b", Events(2), cache.Generation);
        Assert.Equal(BytesOf(3) + BytesOf(2), cache.TotalBytes);

        cache.Put("a", Events(6), cache.Generation); // replace adjusts the running total to the new size
        Assert.Equal(BytesOf(6) + BytesOf(2), cache.TotalBytes);

        cache.Remove("a", cache.Generation);
        Assert.Equal(BytesOf(2), cache.TotalBytes);

        cache.Reset();
        Assert.Equal(0, cache.TotalBytes);
    }

    [Fact]
    public void UnboundedCacheSkipsByteAccounting()
    {
        // Measuring the serialized size on the default (unbounded) path would re-serialize the whole
        // history every turn, reintroducing the very cost this cache exists to avoid. The Go
        // reference guards it the same way (durabletask-go client/worker_history.go).
        var cache = new WorkflowHistoryCache();

        cache.Put("a", Events(3), cache.Generation);
        cache.Put("b", Events(2), cache.Generation);

        Assert.Equal(2, cache.Count); // still cached and usable...
        Assert.NotNull(cache.Get("a"));
        Assert.Equal(0, cache.TotalBytes); // ...just not measured
    }

    [Fact]
    public void TtlSweepIsSliding()
    {
        var clock = new TestClock();
        var cache = new WorkflowHistoryCache(ttl: TimeSpan.FromSeconds(60), clock: clock.Read);

        cache.Put("idle", Events(2), cache.Generation);
        cache.Put("active", Events(2), cache.Generation);

        clock.Advance(TimeSpan.FromSeconds(120)); // past the TTL...
        Assert.NotNull(cache.Get("active")); // ...but a turn refreshes "active"

        cache.SweepExpired();
        Assert.Null(cache.Get("idle"));
        Assert.NotNull(cache.Get("active"));
    }

    [Fact]
    public void NonPositiveConfigUsesDefaults()
    {
        // ttl/maxInstances fall back to their (large) defaults; maxBytes becomes unlimited. None of these
        // should evict the three modest entries below.
        var cache = new WorkflowHistoryCache(
            ttl: TimeSpan.Zero, maxInstances: -1, maxBytes: -5);

        cache.Put("a", Events(1), cache.Generation);
        cache.Put("b", Events(1), cache.Generation);
        cache.Put("c", Events(1), cache.Generation);

        Assert.Equal(3, cache.Count);
        Assert.NotNull(cache.Get("a"));
        Assert.NotNull(cache.Get("b"));
        Assert.NotNull(cache.Get("c"));
    }
}
