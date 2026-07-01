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

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// EF Core save-changes interceptor that captures pending domain events and explicit
/// outbox enqueues, materializes them into <see cref="OutboxMessage"/> rows, and adds
/// them to the <see cref="DbContext"/> so they are written atomically with the user's
/// business entities.
/// </summary>
public sealed class DaprOutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IOutboxMessageFactory factory;
    private readonly IOutboxPendingBuffer buffer;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DaprOutboxSaveChangesInterceptor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprOutboxSaveChangesInterceptor"/> class.
    /// </summary>
    public DaprOutboxSaveChangesInterceptor(
        IOutboxMessageFactory factory,
        IOutboxPendingBuffer buffer,
        TimeProvider timeProvider,
        ILogger<DaprOutboxSaveChangesInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this.factory = factory;
        this.buffer = buffer;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Flush(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Flush(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        using var activity = DaprOutboxDiagnostics.ActivitySource.StartActivity("outbox.flush", ActivityKind.Internal);

        var messages = new List<OutboxMessage>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IHasDomainEvents aggregate && aggregate.DomainEvents.Count > 0)
            {
                foreach (var domainEvent in aggregate.DomainEvents)
                {
                    messages.Add(factory.CreateFromDomainEvent(domainEvent, context));
                }
                aggregate.ClearDomainEvents();
            }
        }

        foreach (var pending in buffer.Drain(context))
        {
            messages.Add(factory.CreateFromExplicit(
                pending.PubSubName,
                pending.Topic,
                pending.Payload,
                pending.Metadata,
                pending.CorrelationId,
                context));
        }

        if (messages.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var message in messages)
        {
            if (message.Id == default)
            {
                message.Id = Guid.NewGuid();
            }
            if (message.OccurredAt == default)
            {
                message.OccurredAt = now;
            }
        }

        context.Set<OutboxMessage>().AddRange(messages);
        activity?.SetTag("outbox.enqueued_count", messages.Count);
        LogEnqueued(logger, messages.Count, context.GetType().Name, null);
    }

    private static readonly Action<ILogger, int, string, Exception?> LogEnqueued =
        LoggerMessage.Define<int, string>(
            LogLevel.Debug,
            new EventId(1, "OutboxMessagesEnqueued"),
            "Enqueued {Count} outbox message(s) for DbContext {DbContext}.");
}
