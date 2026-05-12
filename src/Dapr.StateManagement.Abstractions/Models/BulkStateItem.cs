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

namespace Dapr.StateManagement;

/// <summary>
/// Represents a raw (undeserialized) item returned from a bulk state get operation.
/// </summary>
/// <remarks>
/// Application code does not need to create instances of <see cref="BulkStateItem"/>.
/// Use <see cref="BulkStateItem{TValue}"/> when the value type is known at compile time.
/// </remarks>
/// <param name="key">The state key.</param>
/// <param name="value">The raw UTF-8 JSON value as a string.</param>
/// <param name="etag">The ETag for the item.</param>
public readonly struct BulkStateItem(string key, string value, string etag)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the raw JSON value as a string.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Gets the ETag for optimistic concurrency control.
    /// </summary>
    public string ETag { get; } = etag;
}

/// <summary>
/// Represents a typed item returned from a bulk state get operation.
/// </summary>
/// <remarks>
/// Application code does not need to create instances of <see cref="BulkStateItem{TValue}"/>.
/// </remarks>
/// <typeparam name="TValue">The deserialized type of the value.</typeparam>
/// <param name="key">The state key.</param>
/// <param name="value">The deserialized value of type <typeparamref name="TValue"/>.</param>
/// <param name="etag">The ETag for the item.</param>
public readonly struct BulkStateItem<TValue>(string key, TValue? value, string etag)
{
    /// <summary>
    /// Gets the state key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the deserialized value, or <see langword="null"/> if the key was not found.
    /// </summary>
    public TValue? Value { get; } = value;

    /// <summary>
    /// Gets the ETag for optimistic concurrency control.
    /// </summary>
    public string ETag { get; } = etag;
}
