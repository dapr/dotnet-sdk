// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

#nullable enable
using System;

namespace Dapr.Client;

using System.Collections.Generic;

/// <summary>
/// Represents a single request in in a StateTransaction.
/// </summary>
/// <param name="key">The state key.</param>
/// <param name="value">The serialized state value.</param>
/// <param name="operationType">The operation type.</param>
/// <param name="etag">The etag (optional).</param>
/// <param name="metadata">Additional key value pairs for the state (optional).</param>
/// <param name="options">State options (optional).</param>
public sealed class StateTransactionRequest(string key, byte[]? value, StateOperationType operationType, string? etag = null, IReadOnlyDictionary<string, string>? metadata = null, StateOptions? options = null)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    /// <summary>
    /// Gets or sets the value locally.
    /// </summary>
    public byte[]? Value { get; set; } = value;

    /// <summary>
    /// The Operation type.
    /// </summary>
    public StateOperationType? OperationType { get; set; } = operationType;

    /// <summary>
    /// The ETag (optional).
    /// </summary>
    public string? ETag { get; set; } = etag;

    /// <summary>
    /// Additional key-value pairs to be passed to the state store (optional).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; } = metadata;

    /// <summary>
    /// State Options (optional).
    /// </summary>
    public StateOptions? Options = options;
}
