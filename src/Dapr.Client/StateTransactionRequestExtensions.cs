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
/// Extension methods for shaping <see cref="StateTransactionRequest"/> instances
/// for use with Dapr's transactional outbox feature.
/// </summary>
public static class StateTransactionRequestExtensions
{
    /// <summary>
    /// Returns a new <see cref="StateTransactionRequest"/> whose metadata marks it as an
    /// outbox projection (see <see cref="DaprOutboxMetadata.Projection"/>). The source
    /// request is not mutated.
    /// </summary>
    /// <param name="request">The state transaction request to convert into a projection.</param>
    /// <returns>A new <see cref="StateTransactionRequest"/> with the projection metadata set.</returns>
    public static StateTransactionRequest WithOutboxProjection(this StateTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var merged = MergeMetadata(request.Metadata, new KeyValuePair<string, string>[]
        {
            new(DaprOutboxMetadata.Projection, DaprOutboxMetadata.ProjectionEnabled),
        });

        return new StateTransactionRequest(
            request.Key,
            request.Value,
            request.OperationType ?? StateOperationType.Upsert,
            request.ETag,
            merged,
            request.Options);
    }

    /// <summary>
    /// Returns a new <see cref="StateTransactionRequest"/> with the specified CloudEvent
    /// fields set via metadata. Existing metadata is preserved; only the supplied fields
    /// are overridden. The source request is not mutated.
    /// </summary>
    /// <param name="request">The state transaction request to enrich.</param>
    /// <param name="id">The CloudEvent <c>id</c> field, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="source">The CloudEvent <c>source</c> field, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="type">The CloudEvent <c>type</c> field, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="subject">The CloudEvent <c>subject</c> field, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="dataContentType">The CloudEvent <c>datacontenttype</c> field, or <see langword="null"/> to leave unchanged.</param>
    /// <returns>A new <see cref="StateTransactionRequest"/> with the CloudEvent metadata merged in.</returns>
    public static StateTransactionRequest WithCloudEventOverrides(
        this StateTransactionRequest request,
        string? id = null,
        string? source = null,
        string? type = null,
        string? subject = null,
        string? dataContentType = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var overrides = new List<KeyValuePair<string, string>>(capacity: 5);
        if (id is not null)
        {
            overrides.Add(new KeyValuePair<string, string>(DaprOutboxMetadata.CloudEventId, id));
        }
        if (source is not null)
        {
            overrides.Add(new KeyValuePair<string, string>(DaprOutboxMetadata.CloudEventSource, source));
        }
        if (type is not null)
        {
            overrides.Add(new KeyValuePair<string, string>(DaprOutboxMetadata.CloudEventType, type));
        }
        if (subject is not null)
        {
            overrides.Add(new KeyValuePair<string, string>(DaprOutboxMetadata.CloudEventSubject, subject));
        }
        if (dataContentType is not null)
        {
            overrides.Add(new KeyValuePair<string, string>(DaprOutboxMetadata.CloudEventDataContentType, dataContentType));
        }

        if (overrides.Count == 0)
        {
            // Nothing to change; still return a new instance to keep the API purely non-mutating
            // regardless of caller expectations.
            return new StateTransactionRequest(
                request.Key,
                request.Value,
                request.OperationType ?? StateOperationType.Upsert,
                request.ETag,
                request.Metadata,
                request.Options);
        }

        var merged = MergeMetadata(request.Metadata, overrides);

        return new StateTransactionRequest(
            request.Key,
            request.Value,
            request.OperationType ?? StateOperationType.Upsert,
            request.ETag,
            merged,
            request.Options);
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(
        IReadOnlyDictionary<string, string>? existing,
        IReadOnlyList<KeyValuePair<string, string>> overrides)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        if (existing is not null)
        {
            foreach (var kvp in existing)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        for (var i = 0; i < overrides.Count; i++)
        {
            var kvp = overrides[i];
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }
}
