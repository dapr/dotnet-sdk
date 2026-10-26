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

using Dapr.Common;

namespace Dapr.Search;

/// <summary>
/// Client for Dapr's alpha Search building block. Create a long-lived instance with
/// DaprSearchClientBuilder or AddDaprSearchClient. All operations accept component-specific
/// request metadata and propagate caller cancellation; RPC failures are wrapped in DaprException.
/// </summary>
public abstract class DaprSearchClient : IDaprClient
{
    private bool disposed;

    /// <summary>Creates an index with provider-specific settings supplied through metadata.</summary>
    public abstract Task CreateIndexAsync(string storeName, string index,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Gets an index's approximate document count and provider-specific properties.</summary>
    public abstract Task<SearchIndex> GetIndexAsync(string storeName, string index,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Lists indexes in the named search store.</summary>
    public abstract Task<IReadOnlyList<string>> ListIndexesAsync(string storeName,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes an index and its documents.</summary>
    public abstract Task DeleteIndexAsync(string storeName, string index,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts index-ready documents. Non-object JSON content is reported as an item failure by
    /// the runtime. Omitted options return on acceptance, not necessarily successful indexing.
    /// </summary>
    public abstract Task<IndexDocumentsResult> IndexDocumentsAsync<T>(string storeName, string index,
        IReadOnlyList<SearchDocument<T>> documents, IndexingOptions? options = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Gets found documents in request order, omitting missing IDs. Content is omitted by default.</summary>
    public abstract Task<IReadOnlyList<SearchDocument<T>>> GetDocumentsAsync<T>(string storeName, string index,
        IReadOnlyList<string> ids, bool? includeContent = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes documents. Omitted options return on acceptance rather than completion.</summary>
    public abstract Task<IndexAck> DeleteDocumentsAsync(string storeName, string index,
        IReadOnlyList<string> ids, IndexingOptions? options = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Returns one page of matches. An omitted query matches all documents without returning content.</summary>
    public abstract Task<SearchResult<T>> SearchAsync<T>(string storeName, string index,
        SearchQuery? query = null, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Releases client-owned network resources.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        Dispose(true);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases resources owned by the implementation.</summary>
    protected virtual void Dispose(bool disposing) { }

    /// <summary>Throws when this client has already been disposed.</summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
