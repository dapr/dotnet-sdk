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

namespace Dapr.Vector;

/// <summary>
/// Client for Dapr's alpha Vector API. Embeddings must be supplied by the caller.
/// All operations support request metadata and cancellation; RPC errors are wrapped in DaprException.
/// Payloads use the configured JSON serializer, except byte[] payloads which are transferred unchanged.
/// </summary>
public abstract class DaprVectorClient : IDaprClient
{
    private bool disposed;

    /// <summary>Creates a collection with positive dimensions and a supported metric.</summary>
    public abstract Task CreateCollectionAsync(string storeName, string collection, uint dimensions,
        DistanceMetric metric = DistanceMetric.Unspecified, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the collection's dimensions, effective metric, approximate count and properties.</summary>
    public abstract Task<VectorCollection> GetCollectionAsync(string storeName, string collection,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Lists collections in the named vector store.</summary>
    public abstract Task<IReadOnlyList<string>> ListCollectionsAsync(string storeName,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a collection and its records.</summary>
    public abstract Task DeleteCollectionAsync(string storeName, string collection,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Upserts vectors. Omitted options return on acceptance; inspect acknowledgements and item failures.</summary>
    public abstract Task<UpsertVectorsResult> UpsertVectorsAsync<T>(string storeName, string collection,
        IReadOnlyList<VectorRecord<T>> records, IndexingOptions? options = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Gets found records in request order, omitting missing IDs. Values are omitted by default.</summary>
    public abstract Task<IReadOnlyList<VectorRecord<T>>> GetVectorsAsync<T>(string storeName, string collection,
        IReadOnlyList<string> ids, bool? includeValues = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes records. Omitted options return on acceptance rather than completion.</summary>
    public abstract Task<IndexAck> DeleteVectorsAsync(string storeName, string collection, IReadOnlyList<string> ids,
        IndexingOptions? options = null, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Queries by embedding or stored ID. Values and payloads are omitted by default.</summary>
    public abstract Task<VectorQueryResult<T>> QueryVectorsAsync<T>(string storeName, string collection,
        VectorQuery query, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one response or error per query, in request order. Request-wide failures throw.</summary>
    public abstract Task<IReadOnlyList<VectorBatchQueryResult<T>>> BatchQueryVectorsAsync<T>(
        string storeName, string collection, IReadOnlyList<VectorQuery> queries,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Releases owned network resources.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        Dispose(true);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases implementation-owned resources.</summary>
    protected virtual void Dispose(bool disposing) { }

    /// <summary>Throws if the client has been disposed.</summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
