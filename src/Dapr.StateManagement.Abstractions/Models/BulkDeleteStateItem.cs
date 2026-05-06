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
/// Represents a single item to delete in a bulk state delete operation.
/// </summary>
/// <param name="key">The state key to delete. Must not be null or empty.</param>
/// <param name="etag">
/// The ETag for optimistic concurrency control. When non-<see langword="null"/>, the delete
/// will only succeed if the stored ETag matches this value.
/// </param>
/// <param name="stateOptions">
/// Optional state options controlling consistency and concurrency behavior.
/// </param>
/// <param name="metadata">
/// Optional store-specific metadata for the delete operation.
/// </param>
public readonly struct BulkDeleteStateItem(
    string key,
    string? etag = null,
    StateOptions? stateOptions = null,
    IReadOnlyDictionary<string, string>? metadata = null)
{
    /// <summary>
    /// Gets the state key to delete.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the ETag for optimistic concurrency control, or <see langword="null"/> if not specified.
    /// </summary>
    public string? ETag { get; } = etag;

    /// <summary>
    /// Gets the state options, or <see langword="null"/> to use the store defaults.
    /// </summary>
    public StateOptions? StateOptions { get; } = stateOptions;

    /// <summary>
    /// Gets additional store-specific metadata, or <see langword="null"/> if none.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; } = metadata;
}
