// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.StateManagement;

/// <summary>
/// Defines state management operations scoped to a single Dapr state store.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="DaprStateManagementClient"/>, which requires the state store name on
/// every call, implementations of this interface are pre-bound to a specific store, giving
/// callers a cleaner API and making it easier to swap the underlying store in one place.
/// </para>
/// <para>
/// Use the <see cref="StateStoreAttribute"/> on a <c>partial interface</c> that extends
/// this interface, and the Dapr source generator will emit the concrete implementation and
/// a DI registration extension method automatically.
/// </para>
/// </remarks>
public interface IDaprStateStoreClient
{
    /// <summary>
    /// Retrieves the value of a state entry from the bound state store.
    /// </summary>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
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
    Task<TValue?> GetStateAsync<TValue>(
        string key,
        ConsistencyMode? consistencyMode = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the value and its ETag from the bound state store.
    /// </summary>
    /// <remarks>
    /// The returned ETag can be passed to <see cref="TrySaveStateAsync{TValue}"/> or
    /// <see cref="TryDeleteStateAsync"/> to implement optimistic concurrency control.
    /// </remarks>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
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
    Task<(TValue? Value, string? ETag)> GetStateAndETagAsync<TValue>(
        string key,
        ConsistencyMode? consistencyMode = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple state entries from the bound state store in a single request.
    /// </summary>
    /// <typeparam name="TValue">The type of the state values.</typeparam>
    /// <param name="keys">The keys identifying the state entries to retrieve.</param>
    /// <param name="parallelism">
    /// The maximum degree of parallelism used by the Dapr runtime when fetching the values,
    /// or <see langword="null"/> to use the runtime default.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A read-only list of <see cref="BulkStateItem{TValue}"/>. Items not found will have a
    /// default value.
    /// </returns>
    Task<IReadOnlyList<BulkStateItem<TValue>>> GetBulkStateAsync<TValue>(
        IReadOnlyList<string> keys,
        int? parallelism = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a state entry to the bound state store.
    /// </summary>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
    /// <param name="key">The key identifying the state entry.</param>
    /// <param name="value">The value to save.</param>
    /// <param name="stateOptions">
    /// Optional options controlling consistency and concurrency, or <see langword="null"/> to
    /// use the state store's defaults.
    /// </param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SaveStateAsync<TValue>(
        string key,
        TValue value,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple state entries to the bound state store in a single request.
    /// </summary>
    /// <typeparam name="TValue">The type of the state values.</typeparam>
    /// <param name="items">The state entries to save.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SaveBulkStateAsync<TValue>(
        IReadOnlyList<SaveStateItem<TValue>> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to save a state entry using optimistic concurrency control.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> when the ETag no longer matches the stored value,
    /// indicating a concurrent modification.
    /// </remarks>
    /// <typeparam name="TValue">The type of the state value.</typeparam>
    /// <param name="key">The key identifying the state entry.</param>
    /// <param name="value">The value to save.</param>
    /// <param name="etag">The ETag previously returned by <see cref="GetStateAndETagAsync{TValue}"/>.</param>
    /// <param name="stateOptions">Optional options controlling consistency and concurrency.</param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the save succeeded; <see langword="false"/> if the ETag did
    /// not match.
    /// </returns>
    Task<bool> TrySaveStateAsync<TValue>(
        string key,
        TValue value,
        string etag,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a state entry from the bound state store.
    /// </summary>
    /// <param name="key">The key identifying the state entry to delete.</param>
    /// <param name="stateOptions">Optional options controlling consistency and concurrency.</param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteStateAsync(
        string key,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to delete a state entry using optimistic concurrency control.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> when the ETag no longer matches the stored value.
    /// </remarks>
    /// <param name="key">The key identifying the state entry to delete.</param>
    /// <param name="etag">The ETag previously returned by <see cref="GetStateAndETagAsync{TValue}"/>.</param>
    /// <param name="stateOptions">Optional options controlling consistency and concurrency.</param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the delete succeeded; <see langword="false"/> if the ETag did
    /// not match.
    /// </returns>
    Task<bool> TryDeleteStateAsync(
        string key,
        string etag,
        StateOptions? stateOptions = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple state entries from the bound state store in a single request.
    /// </summary>
    /// <param name="items">The state entries to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteBulkStateAsync(
        IReadOnlyList<BulkDeleteStateItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a set of state operations atomically in a single transaction.
    /// </summary>
    /// <remarks>
    /// Not all state stores support transactions. Check the Dapr documentation for the state
    /// store component you are using.
    /// </remarks>
    /// <param name="operations">The ordered list of operations to execute within the transaction.</param>
    /// <param name="metadata">Optional store-specific metadata to pass with the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task ExecuteStateTransactionAsync(
        IReadOnlyList<StateTransactionRequest> operations,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the bound state store using a JSON query expression.
    /// </summary>
    /// <remarks>
    /// Query support depends on the underlying state store component. Refer to the Dapr
    /// documentation for query syntax and component-specific limitations.
    /// </remarks>
    /// <typeparam name="TValue">The type of the state values.</typeparam>
    /// <param name="jsonQuery">The JSON query expression.</param>
    /// <param name="metadata">Optional store-specific metadata to pass with the request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="StateQueryResponse{TValue}"/> containing the matching items.</returns>
    Task<StateQueryResponse<TValue>> QueryStateAsync<TValue>(
        string jsonQuery,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);
}
