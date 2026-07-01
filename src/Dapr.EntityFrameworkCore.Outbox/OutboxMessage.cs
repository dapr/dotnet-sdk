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

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// A durable outbox row that captures a domain event to be published via Dapr pub/sub.
/// One row is written per event inside the user's <c>SaveChangesAsync</c> transaction,
/// then claimed and published by the outbox dispatcher.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Unique identifier for the message. Emitted as the <c>cloudevent.id</c> metadata
    /// on publish so that consumers can deduplicate across dispatcher retries.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Version of the outbox row schema. Pinned to <c>1</c> for the v1 shape.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// The wall-clock instant at which the event was enqueued.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// The name of the Dapr pub/sub component the event will be published to.
    /// </summary>
    public string PubSubName { get; set; } = string.Empty;

    /// <summary>
    /// The topic on the pub/sub component the event will be published to.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// The MIME type of the payload. Defaults to <c>application/json</c> when produced by the
    /// default message factory; Dapr wraps this payload in a CloudEvent on publish.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// The serialized payload published to the topic.
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// JSON-serialized metadata dictionary applied to the publish call. May contain
    /// keys from <see cref="Dapr.Client.DaprOutboxMetadata"/> (e.g., CloudEvent overrides).
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Optional correlation identifier propagated to consumers via metadata.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The instant at which the message was successfully published, or <see langword="null"/> when unpublished.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// The number of publish attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// The last error message recorded by the dispatcher, or <see langword="null"/> when no error occurred.
    /// A row where <see cref="ProcessedAt"/> is <see langword="null"/>, <see cref="AttemptCount"/> reached
    /// <c>DaprOutboxOptions.MaxAttempts</c>, and <see cref="LastError"/> is set is considered dead-lettered.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Identifier of the dispatcher instance that currently owns a claim on this row.
    /// </summary>
    public string? LockOwner { get; set; }

    /// <summary>
    /// The instant at which the current claim expires; other dispatchers may re-claim the row after this time.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }
}
