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

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Configuration options for the Dapr EF Core outbox.
/// </summary>
public sealed class DaprOutboxOptions
{
    /// <summary>
    /// The default table name used when the caller does not override it.
    /// </summary>
    public const string DefaultTableName = "DaprOutboxMessages";

    /// <summary>
    /// The database schema for the outbox table, or <see langword="null"/> to use the provider default.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// The name of the outbox table. Defaults to <see cref="DefaultTableName"/>.
    /// </summary>
    public string TableName { get; set; } = DefaultTableName;

    /// <summary>
    /// The interval between dispatcher polls when the previous poll returned nothing to publish.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The maximum number of rows a single dispatcher iteration will claim and publish.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// How long a claimed row remains locked before another dispatcher may re-claim it.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The maximum number of publish attempts before a row is considered dead-lettered.
    /// A dead-lettered row is left in the table with <see cref="OutboxMessage.LastError"/>
    /// set for operator inspection; the dispatcher does not attempt to publish it again.
    /// </summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// How long to retain successfully processed rows before the retention hosted service
    /// deletes them. Leave <see langword="null"/> to retain forever.
    /// </summary>
    public TimeSpan? RetentionPeriod { get; set; }

    /// <summary>
    /// When set, the health check reports Unhealthy if the oldest unprocessed row's
    /// <see cref="OutboxMessage.OccurredAt"/> exceeds this age.
    /// </summary>
    public TimeSpan? HealthCheckThreshold { get; set; }

    /// <summary>
    /// The maximum time the hosted service waits for the in-flight batch to finish on
    /// graceful shutdown before returning from <c>StopAsync</c>. Any claims still held
    /// after this timeout will expire naturally after <see cref="LockDuration"/>.
    /// </summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// JSON serializer options used by the default <c>IOutboxMessageFactory</c> when
    /// <see cref="JsonTypeInfoResolver"/> is <see langword="null"/>.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// AOT-safe type-info resolver used by the default <c>IOutboxMessageFactory</c>.
    /// When set, the factory uses this resolver to serialize domain events, avoiding the
    /// reflection-based paths on <see cref="System.Text.Json.JsonSerializer"/>. Recommended
    /// for AOT/trimming scenarios; users pass a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>-derived resolver.
    /// </summary>
    public IJsonTypeInfoResolver? JsonTypeInfoResolver { get; set; }

    /// <summary>
    /// When <see langword="true"/>, <see cref="ModelBuilderExtensions.AddDaprOutbox"/> stores
    /// <see cref="DateTimeOffset"/> columns as UTC ticks (<c>BIGINT</c>) so LINQ predicates
    /// translate on providers like SQLite that lack native <c>datetimeoffset</c> support.
    /// Leave <see langword="false"/> (default) on SQL Server and PostgreSQL to preserve the
    /// provider-native column type. Value is used at model-configuration time only.
    /// </summary>
    public bool UseCompactDateTimeStorage { get; set; }
}
