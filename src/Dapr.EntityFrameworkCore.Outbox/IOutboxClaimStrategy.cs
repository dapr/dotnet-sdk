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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Provider-specific strategy for claiming a batch of unprocessed outbox rows atomically.
/// The default implementation, <see cref="RelationalOutboxClaimStrategy"/>, is portable across
/// EF Core relational providers. Provider-optimized implementations (SQL Server, PostgreSQL)
/// can be registered via <c>IDaprOutboxBuilder.UseClaimStrategy&lt;T&gt;()</c>.
/// </summary>
public interface IOutboxClaimStrategy
{
    /// <summary>
    /// Atomically selects and locks up to <c>options.BatchSize</c> unprocessed messages,
    /// stamping each with <paramref name="lockOwner"/> and a <c>LockedUntil</c> that extends
    /// <c>options.LockDuration</c> into the future. Rows already claimed by another dispatcher
    /// (either via a live lock or a still-valid <c>LockedUntil</c>) must be skipped.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        DbContext dbContext,
        DaprOutboxOptions options,
        string lockOwner,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists the outcome of a dispatched batch: marks succeeded rows with <c>ProcessedAt</c>,
    /// increments <c>AttemptCount</c> and rewrites <c>LockedUntil</c>/<c>LastError</c> on failures.
    /// Also releases any surviving row-level locks held by this dispatcher.
    /// </summary>
    Task ReleaseAsync(
        DbContext dbContext,
        IReadOnlyList<OutboxDispatchResult> results,
        string lockOwner,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a single publish attempt returned by the dispatcher and applied by
/// <see cref="IOutboxClaimStrategy.ReleaseAsync"/>.
/// </summary>
/// <param name="MessageId">The outbox row id.</param>
/// <param name="Succeeded"><c>true</c> when Dapr accepted the publish.</param>
/// <param name="Error">The exception message when <paramref name="Succeeded"/> is <c>false</c>.</param>
/// <param name="NextLockedUntil">
/// When failed, the earliest time the row may be re-claimed. When succeeded, ignored.
/// </param>
/// <param name="AttemptCount">The updated attempt count after this iteration.</param>
public readonly record struct OutboxDispatchResult(
    Guid MessageId,
    bool Succeeded,
    string? Error,
    DateTimeOffset? NextLockedUntil,
    int AttemptCount);
