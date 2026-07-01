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

namespace Dapr.Client;

/// <summary>
/// Fluent builder that produces a list of <see cref="StateTransactionRequest"/> items
/// shaped for Dapr's transactional outbox feature. Each entry pairs the state operation
/// that is persisted to the state store with an optional projection that shapes the
/// payload published to the pub/sub topic.
/// </summary>
/// <remarks>
/// Dapr's outbox requires the projection key to match the state operation key exactly.
/// This builder enforces that invariant at build time.
/// </remarks>
public sealed class OutboxTransactionBuilder
{
    private readonly List<Entry> entries = new();

    /// <summary>
    /// Adds a state upsert whose value is also published as the outbox event.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value">The serialized state value, written to the store and used as the published payload.</param>
    /// <param name="etag">Optional ETag for optimistic concurrency.</param>
    /// <param name="metadata">Optional metadata applied to the state operation.</param>
    /// <param name="options">Optional state options.</param>
    /// <returns>This builder, for chaining.</returns>
    public OutboxTransactionBuilder Upsert(
        string key,
        byte[] value,
        string? etag = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        StateOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        entries.Add(new Entry(
            new StateTransactionRequest(key, value, StateOperationType.Upsert, etag, metadata, options),
            Projection: null));

        return this;
    }

    /// <summary>
    /// Adds a state upsert paired with an explicit projection that shapes the outbox event
    /// payload independently of the state value.
    /// </summary>
    /// <param name="key">The state key. The projection uses the same key.</param>
    /// <param name="stateValue">The value persisted to the state store.</param>
    /// <param name="projectionValue">The value published as the outbox event payload.</param>
    /// <param name="etag">Optional ETag for optimistic concurrency on the state write.</param>
    /// <param name="stateMetadata">Optional metadata applied to the state operation.</param>
    /// <param name="projectionMetadata">
    /// Optional metadata applied to the projection (for example, CloudEvent overrides from
    /// <see cref="DaprOutboxMetadata"/>). The <see cref="DaprOutboxMetadata.Projection"/> key
    /// is always added.
    /// </param>
    /// <param name="options">Optional state options for the state write.</param>
    /// <returns>This builder, for chaining.</returns>
    public OutboxTransactionBuilder UpsertWithProjection(
        string key,
        byte[] stateValue,
        byte[] projectionValue,
        string? etag = null,
        IReadOnlyDictionary<string, string>? stateMetadata = null,
        IReadOnlyDictionary<string, string>? projectionMetadata = null,
        StateOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(stateValue);
        ArgumentNullException.ThrowIfNull(projectionValue);

        var state = new StateTransactionRequest(key, stateValue, StateOperationType.Upsert, etag, stateMetadata, options);
        var projection = new StateTransactionRequest(key, projectionValue, StateOperationType.Upsert, etag: null, projectionMetadata, options: null)
            .WithOutboxProjection();

        entries.Add(new Entry(state, projection));

        return this;
    }

    /// <summary>
    /// Adds a state delete whose absence is signalled to the outbox topic.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="etag">Optional ETag for optimistic concurrency.</param>
    /// <param name="metadata">Optional metadata applied to the state operation.</param>
    /// <param name="options">Optional state options.</param>
    /// <returns>This builder, for chaining.</returns>
    public OutboxTransactionBuilder Delete(
        string key,
        string? etag = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        StateOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        entries.Add(new Entry(
            new StateTransactionRequest(key, value: null, StateOperationType.Delete, etag, metadata, options),
            Projection: null));

        return this;
    }

    /// <summary>
    /// Builds the flat list of <see cref="StateTransactionRequest"/> items in the order
    /// suitable for passing to <see cref="DaprClient.ExecuteStateTransactionAsync"/>.
    /// State operations always precede their paired projections.
    /// </summary>
    /// <returns>An immutable list of transaction requests.</returns>
    public IReadOnlyList<StateTransactionRequest> Build()
    {
        var result = new List<StateTransactionRequest>(entries.Count * 2);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            result.Add(entry.State);
            if (entry.Projection is not null)
            {
                if (!string.Equals(entry.State.Key, entry.Projection.Key, StringComparison.Ordinal))
                {
                    // Defensive: internally we always construct projections with the same key,
                    // so this should never happen. Guard prevents silent corruption if the
                    // internal invariant ever breaks.
                    throw new InvalidOperationException(
                        $"Outbox projection key '{entry.Projection.Key}' does not match state key '{entry.State.Key}'.");
                }

                result.Add(entry.Projection);
            }
        }

        return result;
    }

    private readonly record struct Entry(StateTransactionRequest State, StateTransactionRequest? Projection);
}
