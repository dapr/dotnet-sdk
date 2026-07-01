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

using Microsoft.EntityFrameworkCore;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Materializes <see cref="OutboxMessage"/> rows from either a domain event object or
/// an explicit enqueue call. Implementations are called by the outbox interceptor while
/// the user's <c>SaveChangesAsync</c> transaction is in flight.
/// </summary>
public interface IOutboxMessageFactory
{
    /// <summary>
    /// Creates an <see cref="OutboxMessage"/> from a domain event raised by an aggregate
    /// that implements <see cref="IHasDomainEvents"/>.
    /// </summary>
    /// <param name="domainEvent">The domain event instance.</param>
    /// <param name="dbContext">The <see cref="DbContext"/> whose <c>SaveChangesAsync</c> is executing.</param>
    /// <returns>A populated outbox row (without a persisted <c>Id</c>/<c>OccurredAt</c>; the interceptor assigns those).</returns>
    OutboxMessage CreateFromDomainEvent(object domainEvent, DbContext dbContext);

    /// <summary>
    /// Creates an <see cref="OutboxMessage"/> from an explicit enqueue call made via
    /// <see cref="DbContextExtensions.EnqueueOutbox"/>.
    /// </summary>
    /// <param name="pubSubName">The Dapr pub/sub component name.</param>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="payload">The payload to serialize.</param>
    /// <param name="metadata">Optional metadata to record for the publish call.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="dbContext">The <see cref="DbContext"/> whose <c>SaveChangesAsync</c> is executing.</param>
    /// <returns>A populated outbox row (without a persisted <c>Id</c>/<c>OccurredAt</c>; the interceptor assigns those).</returns>
    OutboxMessage CreateFromExplicit(
        string pubSubName,
        string topic,
        object payload,
        IReadOnlyDictionary<string, string>? metadata,
        string? correlationId,
        DbContext dbContext);
}
