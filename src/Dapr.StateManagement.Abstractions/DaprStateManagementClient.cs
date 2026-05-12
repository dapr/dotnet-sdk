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

namespace Dapr.StateManagement;

/// <summary>
/// <para>
/// Defines client operations for interacting with the Dapr state management building block.
/// </para>
/// <para>
/// Use <c>DaprStateManagementClientBuilder</c> to create a standalone
/// <see cref="DaprStateManagementClient"/>, or register one for dependency injection via
/// <c>DaprStateManagementServiceCollectionExtensions.AddDaprStateManagementClient</c>.
/// </para>
/// <para>
/// Implementations of <see cref="DaprStateManagementClient"/> implement <see cref="IDisposable"/>
/// because the client accesses network resources. For best performance, create a single long-lived
/// client instance and share it for the lifetime of the application — dependency injection handles
/// this automatically. Avoid creating and disposing a client instance per operation as this can
/// lead to socket exhaustion.
/// </para>
/// </summary>
public abstract class DaprStateManagementClient : IDaprClient
{
    private bool disposed;

    /// <summary>
    /// Retrieves the value of a state entry from the named state store.
    /// </summary>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="key">The key identifying the state entry.</param>
    /// <param name="consistencyMode">
    /// The consistency mode for the read operation, or <see langword="null"/> to use the
    /// state store's default.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The deserialized value, or <see langword="null"/> if the key was not found in the store.
    /// </returns>
    public abstract Task<TValue?> GetStateAsync<TValue>(
        string storeName,
        string key,
        ConsistencyMode? consistencyMode = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the value and its ETag from the named state store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned ETag can be passed to <see cref="TrySaveStateAsync{TValue}"/> or
    /// <see cref="TryDeleteStateAsync"/> to implement optimistic concurrency control:
    /// the operation will fail if the ETag no longer matches the stored value.
    /// </para>
    /// </remarks>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="key">The key identifying the state entry.</param>
    /// <param name="consistencyMode">
    /// The consistency mode for the read operation, or <see langword="null"/> to use the
    /// state store's default.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple of the deserialized value (or <see langword="null"/> if not found) and its ETag.
    /// </returns>
    public abstract Task<(TValue? Value, string? ETag)> GetStateAndETagAsync<TValue>(
        string storeName,
        string key,
        ConsistencyMode? consistencyMode = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple state entries from the named state store in a single request.
    /// </summary>
    /// <typeparam name="TValue">The type of the state values.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="keys">The keys identifying the state entries to retrieve.</param>
    /// <param name="parallelism">
    /// The maximum degree of parallelism used by the Dapr runtime when fetching the values,
    /// or <see langword="null"/> to use the runtime default.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A read-only list of <see cref="BulkStateItem{TValue}"/> in the same order as
    /// <paramref name="keys"/>. Items not found in the store will have a default value.
    /// </returns>
    public abstract Task<IReadOnlyList<BulkStateItem<TValue>>> GetBulkStateAsync<TValue>(
        string storeName,
        IReadOnlyList<string> keys,
        int? parallelism = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a state entry to the named state store.
    /// </summary>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="key">The key identifying the state entry.</param>
    /// <param name="value">The value to save. Must be JSON-serializable.</param>
    /// <param name="stateOptions">
    /// Optional options controlling consistency and concurrency, or <see langword="null"/> to
    /// use the state store's defaults.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public abstract Task SaveStateAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple state entries to the named state store in a single request.
    /// </summary>
    /// <typeparam name="TValue">The type of the state values.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="items">The state entries to save.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public abstract Task SaveBulkStateAsync<TValue>(
        string storeName,
        IReadOnlyList<SaveStateItem<TValue>> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to save a state entry using optimistic concurrency control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The save will only succeed if the provided <paramref name="etag"/> matches the ETag
    /// currently stored for the key. Obtain the current ETag by calling
    /// <see cref="GetStateAndETagAsync{TValue}"/>. Returns <see langword="false"/> when the
    /// ETag does not match (i.e., the value was modified since it was last read).
    /// </para>
    /// </remarks>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="key">The key identifying the state entry.</param>
    /// <param name="value">The value to save. Must be JSON-serializable.</param>
    /// <param name="etag">The ETag of the value that was last read from the store.</param>
    /// <param name="stateOptions">
    /// Optional options controlling consistency and concurrency, or <see langword="null"/> to
    /// use the state store's defaults.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the save succeeded; <see langword="false"/> if the ETag did
    /// not match (indicating a concurrent modification).
    /// </returns>
    public abstract Task<bool> TrySaveStateAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a state entry from the named state store.
    /// </summary>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="key">The key identifying the state entry to delete.</param>
    /// <param name="stateOptions">
    /// Optional options controlling consistency and concurrency, or <see langword="null"/> to
    /// use the state store's defaults.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public abstract Task DeleteStateAsync(
        string storeName,
        string key,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to delete a state entry using optimistic concurrency control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delete will only succeed if the provided <paramref name="etag"/> matches the ETag
    /// currently stored for the key. Returns <see langword="false"/> when the ETag does not
    /// match (i.e., the value was modified since it was last read).
    /// </para>
    /// </remarks>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="key">The key identifying the state entry to delete.</param>
    /// <param name="etag">The ETag of the value that was last read from the store.</param>
    /// <param name="stateOptions">
    /// Optional options controlling consistency and concurrency, or <see langword="null"/> to
    /// use the state store's defaults.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the delete succeeded; <see langword="false"/> if the ETag did
    /// not match (indicating a concurrent modification).
    /// </returns>
    public abstract Task<bool> TryDeleteStateAsync(
        string storeName,
        string key,
        string etag,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple state entries from the named state store in a single request.
    /// </summary>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="items">The state entries to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public abstract Task DeleteBulkStateAsync(
        string storeName,
        IReadOnlyList<BulkDeleteStateItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a set of state operations atomically in a single transaction.
    /// </summary>
    /// <remarks>
    /// Not all state stores support transactions. Check the Dapr documentation for the
    /// state store component you are using.
    /// </remarks>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="operations">
    /// The ordered list of operations (upserts and deletes) to execute within the transaction.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public abstract Task ExecuteStateTransactionAsync(
        string storeName,
        IReadOnlyList<StateTransactionRequest> operations,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries a state store using a JSON query expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Query support depends on the underlying state store component. Refer to the Dapr
    /// documentation for query syntax and component-specific limitations.
    /// </para>
    /// <para>
    /// If any items in the result set could not be retrieved, a
    /// <see cref="DaprException"/> is thrown after collecting all successful results.
    /// </para>
    /// </remarks>
    /// <typeparam name="TValue">The type of the state values.</typeparam>
    /// <param name="storeName">The name of the Dapr state store component.</param>
    /// <param name="jsonQuery">The JSON query expression.</param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="StateQueryResponse{TValue}"/> containing the matching items.</returns>
    public abstract Task<StateQueryResponse<TValue>> QueryStateAsync<TValue>(
        string storeName,
        string jsonQuery,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.disposed)
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
            this.disposed = true;
        }
    }

    /// <summary>
    /// Releases the managed resources used by this instance.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> if called from <see cref="Dispose()"/>; otherwise
    /// <see langword="false"/>.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
