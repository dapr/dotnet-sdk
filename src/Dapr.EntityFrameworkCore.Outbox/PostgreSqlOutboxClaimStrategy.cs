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
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// PostgreSQL-optimized claim strategy that uses <c>SELECT ... FOR UPDATE SKIP LOCKED</c>
/// so multiple dispatcher replicas can drain the outbox concurrently without blocking.
/// The claim happens inside a transaction; the row updates are committed before returning.
/// </summary>
public sealed class PostgreSqlOutboxClaimStrategy : IOutboxClaimStrategy
{
    private readonly RelationalOutboxClaimStrategy releaseFallback = new();

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

        var (schema, table) = ResolveTable(dbContext, options);
        var quotedTable = string.IsNullOrEmpty(schema)
            ? $"\"{table}\""
            : $"\"{schema}\".\"{table}\"";

        var lockedUntil = now.Add(options.LockDuration);

        // Two-step CTE keeps the FOR UPDATE SKIP LOCKED semantics and lets us stamp
        // the lock + attempt count and return the updated rows in one round-trip.
        var sql =
            $"WITH candidate AS (\n" +
            $"    SELECT \"Id\"\n" +
            $"    FROM {quotedTable}\n" +
            $"    WHERE \"ProcessedAt\" IS NULL\n" +
            $"      AND \"AttemptCount\" < @maxAttempts\n" +
            $"      AND (\"LockedUntil\" IS NULL OR \"LockedUntil\" < @now)\n" +
            $"    ORDER BY \"OccurredAt\"\n" +
            $"    LIMIT @batchSize\n" +
            $"    FOR UPDATE SKIP LOCKED\n" +
            $")\n" +
            $"UPDATE {quotedTable} AS t\n" +
            $"SET \"LockOwner\" = @owner,\n" +
            $"    \"LockedUntil\" = @lockedUntil,\n" +
            $"    \"AttemptCount\" = t.\"AttemptCount\" + 1\n" +
            $"FROM candidate c\n" +
            $"WHERE t.\"Id\" = c.\"Id\"\n" +
            $"RETURNING t.*;";

        var conn = dbContext.Database.GetDbConnection();
        var opened = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            opened = true;
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            AddParameter(cmd, "@batchSize", options.BatchSize);
            AddParameter(cmd, "@owner", lockOwner);
            AddParameter(cmd, "@lockedUntil", lockedUntil);
            AddParameter(cmd, "@maxAttempts", options.MaxAttempts);
            AddParameter(cmd, "@now", now);

            var claimed = new List<OutboxMessage>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                claimed.Add(Materialize(reader));
            }

            return claimed;
        }
        finally
        {
            if (opened)
            {
                await conn.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public Task ReleaseAsync(
        DbContext dbContext,
        IReadOnlyList<OutboxDispatchResult> results,
        string lockOwner,
        CancellationToken cancellationToken)
        => releaseFallback.ReleaseAsync(dbContext, results, lockOwner, cancellationToken);

    private static (string? Schema, string Table) ResolveTable(DbContext dbContext, DaprOutboxOptions options)
    {
        var entity = dbContext.Model.FindEntityType(typeof(OutboxMessage));
        var schema = entity?.GetSchema() ?? options.SchemaName;
        var table = entity?.GetTableName() ?? options.TableName;
        return (schema, table);
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static OutboxMessage Materialize(DbDataReader reader)
    {
        return new OutboxMessage
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            SchemaVersion = reader.GetInt32(reader.GetOrdinal("SchemaVersion")),
            OccurredAt = ReadDateTimeOffset(reader, "OccurredAt"),
            PubSubName = reader.GetString(reader.GetOrdinal("PubSubName")),
            Topic = reader.GetString(reader.GetOrdinal("Topic")),
            ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
            Payload = (byte[])reader["Payload"],
            MetadataJson = ReadNullableString(reader, "MetadataJson"),
            CorrelationId = ReadNullableString(reader, "CorrelationId"),
            ProcessedAt = ReadNullableDateTimeOffset(reader, "ProcessedAt"),
            AttemptCount = reader.GetInt32(reader.GetOrdinal("AttemptCount")),
            LastError = ReadNullableString(reader, "LastError"),
            LockOwner = ReadNullableString(reader, "LockOwner"),
            LockedUntil = ReadNullableDateTimeOffset(reader, "LockedUntil"),
        };
    }

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            _ => throw new InvalidOperationException($"Unexpected type for column {column}: {value.GetType()}"),
        };
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : ReadDateTimeOffset(reader, column);
    }

    private static string? ReadNullableString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
