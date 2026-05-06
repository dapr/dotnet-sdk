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
/// Represents a single operation within a Dapr state transaction.
/// </summary>
/// <param name="key">The state key. Must not be null or empty.</param>
/// <param name="value">
/// The serialized state value as a byte array, or <see langword="null"/> for delete operations.
/// </param>
/// <param name="operationType">The type of operation to perform.</param>
/// <param name="etag">
/// An optional ETag for optimistic concurrency control. When specified, the operation will
/// only succeed if the stored ETag matches this value.
/// </param>
/// <param name="metadata">Optional store-specific metadata for this operation.</param>
/// <param name="options">Optional state options controlling consistency and concurrency.</param>
public sealed class StateTransactionRequest(
    string key,
    byte[]? value,
    StateOperationType operationType,
    string? etag = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    StateOptions? options = null)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    /// <summary>
    /// Gets or sets the serialized state value, or <see langword="null"/> for delete operations.
    /// </summary>
    public byte[]? Value { get; set; } = value;

    /// <summary>
    /// Gets or sets the type of operation to perform.
    /// </summary>
    public StateOperationType OperationType { get; set; } = operationType;

    /// <summary>
    /// Gets or sets the ETag for optimistic concurrency control, or <see langword="null"/> if not required.
    /// </summary>
    public string? ETag { get; set; } = etag;

    /// <summary>
    /// Gets or sets additional store-specific metadata, or <see langword="null"/> if none.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; } = metadata;

    /// <summary>
    /// Gets or sets state options controlling consistency and concurrency, or <see langword="null"/> to
    /// use the store defaults.
    /// </summary>
    public StateOptions? Options { get; set; } = options;
}
