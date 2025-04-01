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

namespace Dapr.Client;

/// <summary>
/// Represents a state object returned from a bulk get state operation.
/// </summary>
/// <param name="key">The state key.</param>
/// <param name="value">The value.</param>
/// <param name="etag">The ETag.</param>
/// <remarks>
/// Application code should not need to create instances of <see cref="BulkStateItem" />.
/// </remarks>
public readonly struct BulkStateItem(string key, string value, string etag)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Get the ETag.
    /// </summary>
    public string ETag { get; } = etag;
}

/// <summary>
/// Represents a state object returned from a bulk get state operation where the value has
/// been deserialized to the specified type.
/// </summary>
/// <param name="key">The state key.</param>
/// <param name="value">The typed value.</param>
/// <param name="etag">The ETag.</param>
/// <remarks>
/// Application code should not need to create instances of <see cref="BulkStateItem" />.
/// </remarks>
public readonly struct BulkStateItem<TValue>(string key, TValue value, string etag)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the deserialized value of the indicated type.
    /// </summary>
    public TValue Value { get; } = value;

    /// <summary>
    /// Get the ETag.
    /// </summary>
    public string ETag { get; } = etag;
}
