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

namespace Dapr.Vector;

/// <summary>
/// Operations bound to a component, collection and payload type. Annotate a derived partial
/// interface with VectorCollectionAttribute to generate its implementation and DI registration.
/// The wrapper does not own its client. Use byte[] for opaque payloads, other types for JSON.
/// </summary>
public interface IDaprVectorCollection<T>
{
    /// <summary>Creates the bound collection.</summary>
    Task CreateCollectionAsync(uint dimensions, DistanceMetric metric = DistanceMetric.Unspecified,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the bound collection's properties.</summary>
    Task<VectorCollection> GetCollectionAsync(IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the bound collection.</summary>
    Task DeleteCollectionAsync(IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts records; inspect the acknowledgement and item failures.</summary>
    Task<UpsertVectorsResult> UpsertVectorsAsync(IReadOnlyList<VectorRecord<T>> records,
        IndexingOptions? options = null, IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets found records in request order. Values are omitted by default.</summary>
    Task<IReadOnlyList<VectorRecord<T>>> GetVectorsAsync(IReadOnlyList<string> ids, bool? includeValues = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes records, returning the provider's acknowledgement.</summary>
    Task<IndexAck> DeleteVectorsAsync(IReadOnlyList<string> ids, IndexingOptions? options = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Queries by embedding or stored ID.</summary>
    Task<VectorQueryResult<T>> QueryVectorsAsync(VectorQuery query,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>Returns one response or error for each query in request order.</summary>
    Task<IReadOnlyList<VectorBatchQueryResult<T>>> BatchQueryVectorsAsync(IReadOnlyList<VectorQuery> queries,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}
