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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Extension methods on <see cref="DbContext"/> for enqueuing outbox messages explicitly
/// (i.e., without going through the <see cref="IHasDomainEvents"/> convention).
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Enqueues a message for publication via Dapr pub/sub. The message is materialized into
    /// an <see cref="OutboxMessage"/> row when the enclosing <c>SaveChangesAsync</c> call
    /// commits, so it participates in the same database transaction as any other pending changes.
    /// </summary>
    /// <param name="context">The <see cref="DbContext"/> the enqueue is associated with.</param>
    /// <param name="pubSubName">The Dapr pub/sub component name.</param>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="payload">The payload to serialize and publish.</param>
    /// <param name="metadata">Optional metadata applied to the publish call (for example, CloudEvent overrides).</param>
    /// <param name="correlationId">Optional correlation identifier persisted with the outbox row.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <see cref="DbContext"/> was not registered via <c>AddDbContext</c> with an
    /// application service provider, or when <c>AddDaprOutbox&lt;TDbContext&gt;</c> was not called.
    /// </exception>
    public static void EnqueueOutbox(
        this DbContext context,
        string pubSubName,
        string topic,
        object payload,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(pubSubName);
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(payload);

        var buffer = GetApplicationServices(context).GetService<IOutboxPendingBuffer>()
            ?? throw new InvalidOperationException(
                "DaprOutboxSaveChangesInterceptor is not registered. Call AddDaprOutbox<TDbContext>() on your IServiceCollection.");

        buffer.Enqueue(context, new PendingEnqueue(pubSubName, topic, payload, metadata, correlationId));
    }

    private static IServiceProvider GetApplicationServices(DbContext context)
    {
        var options = context.GetService<IDbContextOptions>();
        var core = options.FindExtension<CoreOptionsExtension>();
        return core?.ApplicationServiceProvider
            ?? throw new InvalidOperationException(
                $"DbContext '{context.GetType().Name}' was not registered with an application service provider. " +
                "Register it via IServiceCollection.AddDbContext<TDbContext>(...) and call AddDaprOutbox<TDbContext>().");
    }
}
