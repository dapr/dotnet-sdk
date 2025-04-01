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
using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Represents a state object used for bulk delete state operation
/// </summary>
/// <param name="key">The state key.</param>
/// <param name="etag">The ETag.</param>
/// <param name="stateOptions">The stateOptions.</param>
/// <param name="metadata">The metadata.</param>
public readonly struct BulkDeleteStateItem(string key, string etag, StateOptions? stateOptions = null, IReadOnlyDictionary<string, string>? metadata = null)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Get the ETag.
    /// </summary>
    public string ETag { get; } = etag;

    /// <summary>
    /// Gets the StateOptions.
    /// </summary>
    public StateOptions? StateOptions { get; } = stateOptions;

    /// <summary>
    /// Gets the Metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; } = metadata;
}
