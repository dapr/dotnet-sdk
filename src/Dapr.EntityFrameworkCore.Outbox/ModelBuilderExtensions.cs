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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// EF Core model configuration extensions for the Dapr outbox.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="OutboxMessage"/> entity with the given <see cref="ModelBuilder"/>.
    /// Must be called from <c>DbContext.OnModelCreating</c> for the interceptor to persist
    /// outbox rows into the same transaction as user entities.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="configure">Optional callback to override defaults on <see cref="DaprOutboxOptions"/>.</param>
    /// <returns>The same <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder AddDaprOutbox(this ModelBuilder modelBuilder, Action<DaprOutboxOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var options = new DaprOutboxOptions();
        configure?.Invoke(options);

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            if (options.SchemaName is not null)
            {
                entity.ToTable(options.TableName, options.SchemaName);
            }
            else
            {
                entity.ToTable(options.TableName);
            }

            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedNever();
            entity.Property(o => o.SchemaVersion).IsConcurrencyToken(false);
            entity.Property(o => o.OccurredAt).IsRequired();
            entity.Property(o => o.PubSubName).IsRequired().HasMaxLength(256);
            entity.Property(o => o.Topic).IsRequired().HasMaxLength(256);
            entity.Property(o => o.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(o => o.Payload).IsRequired();
            entity.Property(o => o.CorrelationId).HasMaxLength(128);
            entity.Property(o => o.LockOwner).HasMaxLength(128);

            if (options.UseCompactDateTimeStorage)
            {
                entity.Property(o => o.OccurredAt).HasConversion(DateTimeOffsetConverter);
                entity.Property(o => o.ProcessedAt).HasConversion(NullableDateTimeOffsetConverter);
                entity.Property(o => o.LockedUntil).HasConversion(NullableDateTimeOffsetConverter);
            }

            // Filtered index on unprocessed rows keeps the dispatcher scan cheap even as
            // the table accumulates processed history awaiting retention cleanup.
            ConfigureUnprocessedIndex(entity);
        });

        return modelBuilder;
    }

    // Store DateTimeOffset as UTC ticks (long) so SQLite can order/compare in WHERE clauses
    // without provider-specific translations. Rows written by other providers (SQL Server
    // datetimeoffset, PostgreSQL timestamptz) remain lossless because UtcTicks is monotonic.
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter =
        new(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter =
        new(v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null);

    private static void ConfigureUnprocessedIndex(EntityTypeBuilder<OutboxMessage> entity)
    {
        var index = entity.HasIndex(o => new { o.ProcessedAt, o.OccurredAt }).HasDatabaseName("IX_DaprOutboxMessages_Unprocessed");

        // Filtered indexes are supported by SQL Server, PostgreSQL, and SQLite. Providers
        // that do not recognise the filter (e.g., in-memory) fall back to a full index.
        index.HasFilter("[ProcessedAt] IS NULL");
    }
}
