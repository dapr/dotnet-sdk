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

namespace Dapr.VirtualActors;

/// <summary>
/// Represents a value that may or may not be present.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="HasValue">
/// <see langword="true"/> if the value is present; otherwise <see langword="false"/>.
/// </param>
/// <param name="Value">
/// The value, if present; otherwise <c>default(T)</c>.
/// </param>
public readonly record struct ConditionalValue<T>(bool HasValue, T Value)
{
    /// <summary>
    /// Gets a <see cref="ConditionalValue{T}"/> that represents the absence of a value.
    /// </summary>
    public static ConditionalValue<T> None => new(false, default!);

    /// <summary>
    /// Creates a <see cref="ConditionalValue{T}"/> that contains the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A <see cref="ConditionalValue{T}"/> containing <paramref name="value"/>.</returns>
    public static ConditionalValue<T> Some(T value) => new(true, value);
}
