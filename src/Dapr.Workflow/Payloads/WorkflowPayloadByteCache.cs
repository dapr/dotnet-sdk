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
using System.Linq;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Replica-local acceleration for immutable, hash-verified payloads. Entries are not evidence
/// that an object still exists in shared storage, and cache misses must always read that storage.
/// </summary>
internal sealed class WorkflowPayloadByteCache(
    long capacity = 64 * 1024 * 1024,
    TimeProvider? timeProvider = null)
{
    internal const string SwitchName = "Dapr.Workflow.EnablePayloadByteCache";
    private readonly object _lock = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<Key, LinkedListNode<Entry>> _entries = new();
    private readonly LinkedList<Entry> _lru = new();
    private long _size;
    private long _generation;

    internal long Generation
    {
        get
        {
            lock (_lock) return _generation;
        }
    }

    internal bool TryGet(PayloadReference reference, out ReadOnlyMemory<byte> bytes)
    {
        lock (_lock)
        {
            if (reference.Stages.Count == 0 && _entries.TryGetValue(Key.From(reference), out var node))
            {
                if (_time.GetElapsedTime(node.Value.Created) < TimeSpan.FromMinutes(1))
                {
                    _lru.Remove(node);
                    _lru.AddLast(node);
                    bytes = node.Value.Bytes;
                    return true;
                }

                Remove(node);
            }
        }

        bytes = default;
        return false;
    }

    internal void Add(PayloadReference reference, ReadOnlyMemory<byte> bytes, int retainedBytes, long generation)
    {
        if (reference.Stages.Count != 0 || retainedBytes > capacity)
            return;

        lock (_lock)
        {
            // A read begun before cleanup must not repopulate the cache afterwards.
            if (_generation != generation)
                return;

            var key = Key.From(reference);
            if (_entries.TryGetValue(key, out var existing))
                Remove(existing);

            while (_lru.First is { } oldest &&
                   (_size + retainedBytes > capacity || _entries.Count >= 256 ||
                    _time.GetElapsedTime(oldest.Value.Created) >= TimeSpan.FromMinutes(1)))
            {
                Remove(oldest);
            }

            _entries.Add(key, _lru.AddLast(new Entry(key, bytes, retainedBytes, _time.GetTimestamp())));
            _size += retainedBytes;
        }
    }

    internal void Invalidate(PayloadReference reference)
    {
        lock (_lock)
        {
            _generation++;
            foreach (var node in _entries.Values.Where(n =>
                         n.Value.Key.Store == reference.StoreName && n.Value.Key.Name == reference.Key).ToArray())
            {
                Remove(node);
            }
        }
    }

    private void Remove(LinkedListNode<Entry> node)
    {
        _entries.Remove(node.Value.Key);
        _lru.Remove(node);
        _size -= node.Value.RetainedBytes;
    }

    private readonly record struct Key(string Store, string Name, long Size, string Hash, int Version)
    {
        internal static Key From(PayloadReference reference) =>
            new(reference.StoreName, reference.Key, reference.Size, reference.ContentHash, reference.Version);
    }

    private sealed record Entry(Key Key, ReadOnlyMemory<byte> Bytes, int RetainedBytes, long Created);
}
