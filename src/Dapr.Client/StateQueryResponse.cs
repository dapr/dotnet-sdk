/*
Copyright 2021 The Dapr Authors
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

#nullable enable
using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Represents the response from a state query.
/// </summary>
/// <param name="results">The results of the query.</param>
/// <param name="token">The pagination token to continue the query.</param>
/// <param name="metadata">The metadata to be passed back to the caller.</param>
public class StateQueryResponse<TValue>(IReadOnlyList<StateQueryItem<TValue>> results, string token, IReadOnlyDictionary<string, string> metadata)
{
    /// <summary>
    /// The results of the query.
    /// </summary>
    public IReadOnlyList<StateQueryItem<TValue>> Results { get; } = new List<StateQueryItem<TValue>>(results);

    /// <summary>
    /// The pagination token to continue the query.
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// The metadata to be passed back to the caller.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; } = metadata;
}

/// <summary>
/// Represents an individual item from the results of a state query.
/// </summary>
/// <param name="key">The key of the returned item.</param>
/// <param name="data">The value of the returned item.</param>
/// <param name="etag">The ETag of the returned item.</param>
/// <param name="error">The error, if one occurred, of the returned item.</param>
public class StateQueryItem<TValue>(string key, TValue data, string etag, string error)
{
    /// <summary>
    /// The key from the matched query.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// The data of the key from the matched query.
    /// </summary>
    public TValue? Data { get; } = data;

    /// <summary>
    /// The ETag for the key from the matched query.
    /// </summary>
    public string ETag { get; } = etag;

    /// <summary>
    /// The error from the query, if one occurred.
    /// </summary>
    public string Error { get; } = error;
}
