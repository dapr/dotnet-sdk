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

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Default in-memory implementation of <see cref="IOutboxPendingBuffer"/>.
/// Uses a <see cref="ConditionalWeakTable{TKey,TValue}"/> so pending lists are garbage-collected
/// together with their owning <see cref="DbContext"/>.
/// </summary>
public sealed class OutboxPendingBuffer : IOutboxPendingBuffer
{
    // ConditionalWeakTable ensures pending lists are garbage-collected together with their
    // owning DbContext, so long-running singletons never leak entries after a scope exits.
    private readonly ConditionalWeakTable<DbContext, List<PendingEnqueue>> pending = new();

    /// <inheritdoc />
    public void Enqueue(DbContext context, PendingEnqueue pendingEnqueue)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pendingEnqueue);

        var list = pending.GetValue(context, static _ => new List<PendingEnqueue>());
        lock (list)
        {
            list.Add(pendingEnqueue);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PendingEnqueue> Drain(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!pending.TryGetValue(context, out var list))
        {
            return Array.Empty<PendingEnqueue>();
        }

        PendingEnqueue[] snapshot;
        lock (list)
        {
            if (list.Count == 0)
            {
                return Array.Empty<PendingEnqueue>();
            }
            snapshot = list.ToArray();
            list.Clear();
        }
        return snapshot;
    }
}
