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

/// <summary>
/// Search operations bound to one component, index and document type. Annotate a
/// derived partial interface with <see cref="SearchIndexAttribute"/> to generate its
/// implementation and DI registration. The wrapper does not own or dispose its client.
/// </summary>
/// <typeparam name="T">The document content type, serialized with the client's JSON options.</typeparam>
public interface IDaprSearchIndex<T>
{
    /// <summary>Creates the bound index with provider-specific metadata settings.</summary>
    Task CreateIndexAsync(IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the bound index's count and provider-specific properties.</summary>
    Task<SearchIndex> GetIndexAsync(IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the bound index and all its documents.</summary>
    Task DeleteIndexAsync(IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts documents. Inspect both the acknowledgement and per-item failures.</summary>
    Task<IndexDocumentsResult> IndexDocumentsAsync(IReadOnlyList<SearchDocument<T>> documents,
        IndexingOptions? options = null, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets found documents in request order, omitting missing IDs. Content is omitted by default.</summary>
    Task<IReadOnlyList<SearchDocument<T>>> GetDocumentsAsync(IReadOnlyList<string> ids,
        bool? includeContent = null, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes documents, returning the provider's write acknowledgement.</summary>
    Task<IndexAck> DeleteDocumentsAsync(IReadOnlyList<string> ids, IndexingOptions? options = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Gets one page of matches. An omitted query matches all documents without content.</summary>
    Task<SearchResult<T>> SearchAsync(SearchQuery? query = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}
