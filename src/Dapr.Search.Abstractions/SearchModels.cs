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

namespace Dapr.Search;

/// <summary>An index-ready document. Content must serialize to a JSON object for indexing.</summary>
/// <param name="Id">Caller-supplied identifier, unique within the index.</param>
/// <param name="Content">Document content, or default when content was not requested.</param>
/// <param name="Metadata">Opaque metadata stored with the document; not indexed or filterable.</param>
public sealed record SearchDocument<T>(
    string Id,
    T? Content = default,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>A search match.</summary>
/// <param name="Document">The matching document.</param>
/// <param name="Score">Provider-specific, unnormalized relevance score.</param>
/// <param name="Highlights">Highlighted fragments keyed by field name.</param>
public sealed record SearchHit<T>(
    SearchDocument<T> Document,
    double Score,
    IReadOnlyDictionary<string, string> Highlights);

/// <summary>Information about an index.</summary>
/// <param name="Index">Index name.</param>
/// <param name="DocumentCount">Approximate count, or zero when unavailable.</param>
/// <param name="Properties">Provider-specific properties.</param>
public sealed record SearchIndex(string Index, ulong DocumentCount, IReadOnlyDictionary<string, string> Properties);

/// <summary>Accuracy of the reported total hit count.</summary>
public enum TotalHitsRelation
{
    /// <summary>No accuracy information is available.</summary>
    Unspecified = 0,

    /// <summary>The total is exact.</summary>
    Exact = 1,

    /// <summary>The total is a lower bound.</summary>
    LowerBound = 2,

    /// <summary>The total is an estimate.</summary>
    Estimate = 3
}

/// <summary>A single page of search results.</summary>
/// <param name="Hits">Matches in provider order.</param>
/// <param name="TotalHits">Best-effort total, or null when unavailable.</param>
/// <param name="TotalHitsRelation">Accuracy of the total.</param>
/// <param name="ContinuationToken">Opaque next-page token, or null on the last page.</param>
public sealed record SearchResult<T>(
    IReadOnlyList<SearchHit<T>> Hits,
    ulong? TotalHits,
    TotalHitsRelation TotalHitsRelation,
    string? ContinuationToken);

/// <summary>The acknowledgement boundary reached by an indexing or deletion operation.</summary>
public enum IndexAck
{
    /// <summary>The provider accepted background work; eventual success is not guaranteed.</summary>
    Queued = 1,

    /// <summary>The write completed; indexing results contain every item-specific failure.</summary>
    Completed = 2
}

/// <summary>A serialized protobuf Any from a provider error, preserved without interpretation.</summary>
/// <param name="TypeUrl">The protobuf message type URL.</param>
/// <param name="Value">Serialized message bytes.</param>
public sealed record SearchErrorDetail(string TypeUrl, ReadOnlyMemory<byte> Value);

/// <summary>An item-specific indexing failure known at the acknowledgement boundary.</summary>
/// <param name="Id">The failed document's identifier.</param>
/// <param name="ErrorCode">Canonical google.rpc.Code value.</param>
/// <param name="ErrorMessage">Error description.</param>
/// <param name="Details">Provider-specific error details.</param>
public sealed record SearchFailedItem(
    string Id,
    int ErrorCode,
    string ErrorMessage,
    IReadOnlyList<SearchErrorDetail> Details);

/// <summary>The result of submitting a batch of documents.</summary>
/// <param name="Ack">Whether the batch was queued or completed.</param>
/// <param name="FailedItems">Known failures. An empty list with Queued does not guarantee eventual success.</param>
public sealed record IndexDocumentsResult(IndexAck Ack, IReadOnlyList<SearchFailedItem> FailedItems);
