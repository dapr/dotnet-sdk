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
/// Represents the response from a state store query operation.
/// </summary>
/// <typeparam name="TValue">The type of the state values in the query results.</typeparam>
/// <param name="results">The items returned by the query.</param>
/// <param name="token">
/// A pagination token used to continue the query from where it left off.
/// An empty string or <see langword="null"/> indicates there are no more results.
/// </param>
/// <param name="metadata">Additional metadata returned by the state store.</param>
public sealed class StateQueryResponse<TValue>(
    IReadOnlyList<StateQueryItem<TValue>> results,
    string? token,
    IReadOnlyDictionary<string, string> metadata)
{
    /// <summary>
    /// Gets the list of items matching the query.
    /// </summary>
    public IReadOnlyList<StateQueryItem<TValue>> Results { get; } = results;

    /// <summary>
    /// Gets the pagination token to continue the query, or <see langword="null"/> / empty string
    /// if there are no further results.
    /// </summary>
    public string? Token { get; } = token;

    /// <summary>
    /// Gets additional metadata returned by the state store.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; } = metadata;
}

/// <summary>
/// Represents a single item in the results of a state store query.
/// </summary>
/// <typeparam name="TValue">The type of the state value.</typeparam>
/// <param name="key">The state key of the matched item.</param>
/// <param name="data">The deserialized value of the matched item.</param>
/// <param name="etag">The ETag of the matched item.</param>
/// <param name="error">
/// An error message if this item could not be retrieved, or <see langword="null"/> / empty
/// if the item was retrieved successfully.
/// </param>
public sealed class StateQueryItem<TValue>(string key, TValue? data, string? etag, string? error)
{
    /// <summary>
    /// Gets the state key of the matched item.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the deserialized value of the matched item, or <see langword="null"/> if an error occurred.
    /// </summary>
    public TValue? Data { get; } = data;

    /// <summary>
    /// Gets the ETag for the matched item.
    /// </summary>
    public string? ETag { get; } = etag;

    /// <summary>
    /// Gets the error message if this item could not be retrieved, or <see langword="null"/> / empty
    /// on success.
    /// </summary>
    public string? Error { get; } = error;
}
