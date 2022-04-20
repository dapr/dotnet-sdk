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

using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Represents the response from a state query.
    /// </summary>
    public class StateQueryResponse<TValue>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="results">The results of the query.</param>
        /// <param name="token">The pagination token to continue the query.</param>
        /// <param name="metadata">The metadata to be passed back to the caller.</param>
        public StateQueryResponse(IReadOnlyList<StateQueryItem<TValue>> results, string token, IReadOnlyDictionary<string, string> metadata)
        {
            Results = new List<StateQueryItem<TValue>>(results);
            Token = token;
            Metadata = metadata;
        }

        /// <summary>
        /// The results of the query.
        /// </summary>
        public IReadOnlyList<StateQueryItem<TValue>> Results { get; }

        /// <summary>
        /// The pagination token to continue the query.
        /// </summary>
        public string Token { get; }

        /// <summary>
        /// The metadata to be passed back to the caller.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }

    /// <summary>
    /// Represents an individual item from the results of a state query.
    /// </summary>
    public class StateQueryItem<TValue>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="key">The key of the returned item.</param>
        /// <param name="data">The value of the returned item.</param>
        /// <param name="etag">The ETag of the returned item.</param>
        /// <param name="error">The error, if one occurred, of the returned item.</param>
        public StateQueryItem(string key, TValue data, string etag, string error)
        {
            Key = key;
            Data = data;
            ETag = etag;
            Error = error;
        }

        /// <summary>
        /// The key from the matched query.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The data of the the key from the matched query.
        /// </summary>
        public TValue Data { get; }

        /// <summary>
        /// The ETag for the key from the matched query.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// The error from the query, if one occurred.
        /// </summary>
        public string Error { get; }
    }
}
