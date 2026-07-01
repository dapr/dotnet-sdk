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
/// Records an explicit outbox enqueue made via <see cref="DbContextExtensions.EnqueueOutbox"/>
/// so that the interceptor can materialize an <see cref="OutboxMessage"/> row when the associated
/// <see cref="DbContext"/> saves.
/// </summary>
/// <param name="PubSubName">The Dapr pub/sub component name.</param>
/// <param name="Topic">The topic to publish to.</param>
/// <param name="Payload">The payload to serialize.</param>
/// <param name="Metadata">Optional metadata to apply to the publish call.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
public sealed record PendingEnqueue(
    string PubSubName,
    string Topic,
    object Payload,
    IReadOnlyDictionary<string, string>? Metadata,
    string? CorrelationId);
