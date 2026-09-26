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

using System;
using System.Collections.Generic;
using Dapr.DurableTask.Protobuf;

namespace Dapr.Workflow.Worker.Grpc;

/// <summary>
/// Per-stream cache of each workflow instance's committed history, enabling
/// "stateful history" delta work items: a worker advertising
/// <c>WORKER_CAPABILITY_STATEFUL_HISTORY</c> retains the history it replayed so the
/// sidecar can send only the new events (the delta). Entries are reclaimed by a
/// sliding TTL, an instance-count cap, and an optional byte budget (LRU eviction).
/// Eviction is always safe: a miss is recovered via the GetInstanceHistory RPC.
/// </summary>
/// <remarks>Thread-safe; work items are processed concurrently.</remarks>
internal sealed class WorkflowHistoryCache
{
    private const int DefaultMaxInstances = 100_000;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    private sealed class Entry
    {
        public required IReadOnlyList<HistoryEvent> Events { get; init; }

        /// <summary>Matches the width of the running total and the configured budget, so a large
        /// history cannot overflow and corrupt the accounting.</summary>
        public required long Bytes { get; init; }
        public DateTime LastAccess { get; set; }
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly TimeSpan _ttl;
    private readonly int _maxInstances;
    private readonly long _maxBytes;
    private readonly Func<DateTime> _clock;
    private long _totalBytes;

    // Bumped by every Reset, i.e. once per work-item stream. Writes carry the generation of the
    // stream they belong to so a work item that outlives its stream cannot repopulate the cache
    // behind the next one: ReceiveLoopAsync abandons in-flight tasks on cancellation, and a
    // handler that ignores the token can finish long after Reset has run.
    private int _generation;

    /// <summary>The current stream generation; pass it back to <see cref="Put"/>/<see cref="Remove"/>.</summary>
    public int Generation
    {
        get
        {
            lock (_lock)
            {
                return _generation;
            }
        }
    }

    /// <summary>Initializes the cache. Non-positive ttl/maxInstances use defaults; maxBytes &lt;= 0 means unlimited.</summary>
    public WorkflowHistoryCache(
        TimeSpan? ttl = null,
        int maxInstances = 0,
        long maxBytes = 0,
        Func<DateTime>? clock = null)
    {
        _ttl = ttl is { } configured && configured > TimeSpan.Zero ? configured : DefaultTtl;
        _maxInstances = maxInstances > 0 ? maxInstances : DefaultMaxInstances;
        _maxBytes = maxBytes > 0 ? maxBytes : 0;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>Returns the cached committed history for an instance, refreshing its TTL, or null on a miss.</summary>
    public IReadOnlyList<HistoryEvent>? Get(string instanceId)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(instanceId, out var entry))
            {
                return null;
            }

            entry.LastAccess = _clock();
            return entry.Events;
        }
    }

    /// <summary>
    /// Caches an instance's committed history, evicting LRU entries to stay within bounds. Ignored
    /// when <paramref name="generation"/> is not the current one, i.e. the writing work item's
    /// stream has already been retired.
    /// </summary>
    public void Put(string instanceId, IEnumerable<HistoryEvent> events, int generation)
    {
        var snapshot = new List<HistoryEvent>(events);

        // Only measure the serialized size when a byte budget is configured. On the default
        // (unbounded) path this avoids re-serializing the whole history every turn, which would
        // reintroduce the full-history serialization cost this cache exists to avoid.
        long bytes = 0;
        if (_maxBytes > 0)
        {
            foreach (var historyEvent in snapshot)
            {
                bytes += historyEvent.CalculateSize();
            }
        }

        lock (_lock)
        {
            if (generation != _generation)
            {
                return;
            }

            if (_entries.TryGetValue(instanceId, out var existing))
            {
                _totalBytes -= existing.Bytes;
            }

            _entries[instanceId] = new Entry { Events = snapshot, Bytes = bytes, LastAccess = _clock() };
            _totalBytes += bytes;
            EvictToFit(instanceId);
        }
    }

    /// <summary>
    /// Drops an instance's cached history (e.g. once it completes). Ignored when
    /// <paramref name="generation"/> is not the current one, so a retired stream's late work item
    /// cannot evict an entry the current stream is relying on.
    /// </summary>
    public void Remove(string instanceId, int generation)
    {
        lock (_lock)
        {
            if (generation != _generation)
            {
                return;
            }

            RemoveLocked(instanceId);
        }
    }

    /// <summary>
    /// Clears the cache and retires the current generation; used when the stream reconnects (and
    /// starts cold).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _entries.Clear();
            _totalBytes = 0;
            _generation++;
        }
    }

    /// <summary>Evicts entries whose last turn was longer ago than the TTL.</summary>
    public void SweepExpired()
    {
        var now = _clock();
        lock (_lock)
        {
            var expired = new List<string>();
            foreach (var (instanceId, entry) in _entries)
            {
                if (now - entry.LastAccess > _ttl)
                {
                    expired.Add(instanceId);
                }
            }

            foreach (var instanceId in expired)
            {
                RemoveLocked(instanceId);
            }
        }
    }

    internal int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    internal long TotalBytes
    {
        get
        {
            lock (_lock)
            {
                return _totalBytes;
            }
        }
    }

    private void RemoveLocked(string instanceId)
    {
        if (_entries.Remove(instanceId, out var entry))
        {
            _totalBytes -= entry.Bytes;
        }
    }

    /// <summary>
    /// Evicts least-recently-used entries until within the count and byte bounds. Always keeps the
    /// just-touched entry so the active working set is never evicted; a lone entry over the byte
    /// budget is kept (a soft overage) rather than thrashing.
    /// </summary>
    private void EvictToFit(string keep)
    {
        while (_entries.Count > 1)
        {
            var overCount = _entries.Count > _maxInstances;
            var overBytes = _maxBytes > 0 && _totalBytes > _maxBytes;
            if (!overCount && !overBytes)
            {
                return;
            }

            var victim = LeastRecentlyUsedExcept(keep);
            if (victim is null)
            {
                return;
            }

            RemoveLocked(victim);
        }
    }

    private string? LeastRecentlyUsedExcept(string keep)
    {
        string? oldestId = null;
        var oldestAccess = DateTime.MaxValue;
        foreach (var (instanceId, entry) in _entries)
        {
            if (instanceId == keep)
            {
                continue;
            }

            if (oldestId is null || entry.LastAccess < oldestAccess)
            {
                oldestId = instanceId;
                oldestAccess = entry.LastAccess;
            }
        }

        return oldestId;
    }
}
