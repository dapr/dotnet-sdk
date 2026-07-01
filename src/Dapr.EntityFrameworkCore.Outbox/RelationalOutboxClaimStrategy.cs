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

#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Portable claim strategy that works with any EF Core relational provider.
/// Uses a serializable transaction to select unlocked rows, stamps them with the current
/// lock owner, and commits before returning. Not optimal for high concurrency; use
/// <c>SqlServerOutboxClaimStrategy</c> or <c>PostgreSqlOutboxClaimStrategy</c> on those
/// providers for provider-optimized concurrent claiming.
/// </summary>
public sealed class RelationalOutboxClaimStrategy : IOutboxClaimStrategy
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        DbContext dbContext,
        DaprOutboxOptions options,
        string lockOwner,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(lockOwner);

        var lockedUntil = now.Add(options.LockDuration);

        await using var tx = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        var set = dbContext.Set<OutboxMessage>();

        var candidates = await set
            .Where(m => m.ProcessedAt == null
                        && m.AttemptCount < options.MaxAttempts
                        && (m.LockedUntil == null || m.LockedUntil < now))
            .OrderBy(m => m.OccurredAt)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Array.Empty<OutboxMessage>();
        }

        foreach (var m in candidates)
        {
            m.LockOwner = lockOwner;
            m.LockedUntil = lockedUntil;
            m.AttemptCount += 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return candidates;
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(
        DbContext dbContext,
        IReadOnlyList<OutboxDispatchResult> results,
        string lockOwner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrEmpty(lockOwner);

        if (results.Count == 0)
        {
            return;
        }

        var ids = results.Select(r => r.MessageId).ToArray();
        var rows = await dbContext.Set<OutboxMessage>()
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var byId = rows.ToDictionary(r => r.Id);

        foreach (var result in results)
        {
            if (!byId.TryGetValue(result.MessageId, out var row))
            {
                continue;
            }

            if (result.Succeeded)
            {
                row.ProcessedAt = DateTimeOffset.UtcNow;
                row.LockOwner = null;
                row.LockedUntil = null;
                row.LastError = null;
            }
            else
            {
                row.LastError = result.Error;
                row.AttemptCount = result.AttemptCount;
                row.LockedUntil = result.NextLockedUntil;
                row.LockOwner = null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
