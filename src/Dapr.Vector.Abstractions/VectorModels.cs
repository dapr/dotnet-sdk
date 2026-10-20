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

namespace Dapr.Vector;

/// <summary>A dense vector with an optional payload and structured, filterable metadata.</summary>
/// <param name="Id">Caller-supplied identifier, unique within the collection.</param>
/// <param name="Values">Pre-computed embeddings; length must match the collection dimensions. Empty when not requested.</param>
/// <param name="Payload">Optional payload. Use byte[] for opaque bytes; other types use the client's JSON options.</param>
/// <param name="Metadata">Optional JSON object addressed by the portable filter DSL, separate from request metadata.</param>
public sealed record VectorRecord<T>(
    string Id,
    IReadOnlyList<float> Values,
    T? Payload = default,
    JsonElement? Metadata = null);

/// <summary>The unnormalized metric used to compare vectors.</summary>
public enum DistanceMetric
{
    /// <summary>Use the component default when creating a collection, or the collection metric when querying.</summary>
    Unspecified = 0,

    /// <summary>Cosine similarity; higher scores are closer.</summary>
    Cosine = 1,

    /// <summary>Dot-product similarity; higher scores are closer.</summary>
    DotProduct = 2,

    /// <summary>Euclidean distance; lower scores are closer.</summary>
    Euclidean = 3
}

/// <summary>Collection properties reported by the provider.</summary>
/// <param name="Collection">Collection name.</param>
/// <param name="RecordCount">Approximate count, or zero when unavailable.</param>
/// <param name="Properties">Component-specific properties.</param>
/// <param name="Dimensions">Length of vectors stored in this collection.</param>
/// <param name="Metric">Effective, concrete distance metric.</param>
public sealed record VectorCollection(
    string Collection,
    ulong RecordCount,
    IReadOnlyDictionary<string, string> Properties,
    uint Dimensions,
    DistanceMetric Metric);

/// <summary>A vector match.</summary>
/// <param name="Record">The matching record, shaped by the query's inclusion options.</param>
/// <param name="Score">Unnormalized score interpreted using the result's metric.</param>
public sealed record VectorMatch<T>(VectorRecord<T> Record, double Score);

/// <summary>The result of a vector query. Pagination is not supported in alpha1.</summary>
/// <param name="Matches">Matches in provider order.</param>
/// <param name="Metric">Effective metric used to interpret scores.</param>
public sealed record VectorQueryResult<T>(IReadOnlyList<VectorMatch<T>> Matches, DistanceMetric Metric);

/// <summary>A serialized protobuf Any, preserved without interpretation.</summary>
/// <param name="TypeUrl">Protobuf message type URL.</param>
/// <param name="Value">Serialized message bytes.</param>
public sealed record VectorErrorDetail(string TypeUrl, ReadOnlyMemory<byte> Value);

/// <summary>A per-query error that does not discard other batch results.</summary>
/// <param name="Code">Non-OK canonical google.rpc.Code.</param>
/// <param name="Message">Error description.</param>
/// <param name="Details">Provider-specific details.</param>
public sealed record VectorQueryError(int Code, string Message, IReadOnlyList<VectorErrorDetail> Details);

/// <summary>One ordered batch outcome. Exactly one of Response and Error is populated.</summary>
public sealed class VectorBatchQueryResult<T>
{
    /// <summary>Creates a successful query outcome.</summary>
    public VectorBatchQueryResult(VectorQueryResult<T> response) =>
        Response = response ?? throw new ArgumentNullException(nameof(response));

    /// <summary>Creates a failed query outcome.</summary>
    public VectorBatchQueryResult(VectorQueryError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Code == 0)
            throw new ArgumentException("An error must have a non-OK status.", nameof(error));
        Error = error;
    }

    /// <summary>The query response, or null when this query failed.</summary>
    public VectorQueryResult<T>? Response { get; }

    /// <summary>The query error, or null when this query succeeded.</summary>
    public VectorQueryError? Error { get; }
}

/// <summary>The acknowledgement boundary reached by a vector write.</summary>
public enum IndexAck
{
    /// <summary>Accepted background work; eventual success is not guaranteed.</summary>
    Queued = 1,

    /// <summary>The write completed; upsert results contain all item-specific failures.</summary>
    Completed = 2
}

/// <summary>An item failure known at the acknowledgement boundary.</summary>
/// <param name="Id">Failed record identifier.</param>
/// <param name="ErrorCode">Non-OK canonical google.rpc.Code.</param>
/// <param name="ErrorMessage">Error description.</param>
/// <param name="Details">Provider-specific details.</param>
public sealed record VectorFailedItem(
    string Id,
    int ErrorCode,
    string ErrorMessage,
    IReadOnlyList<VectorErrorDetail> Details);

/// <summary>The result of submitting vectors.</summary>
/// <param name="Ack">Queued or completed.</param>
/// <param name="FailedItems">Known failures. Empty failures with Queued do not guarantee eventual success.</param>
public sealed record UpsertVectorsResult(IndexAck Ack, IReadOnlyList<VectorFailedItem> FailedItems);
