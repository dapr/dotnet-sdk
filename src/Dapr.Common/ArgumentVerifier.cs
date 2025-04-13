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

// TODO: Remove this when every project that uses this file has nullable enabled.
#nullable enable

namespace Dapr;

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A utility class to perform argument validations. 
/// </summary>
internal static class ArgumentVerifier
{
    /// <summary>
    /// Throws ArgumentNullException if argument is null.
    /// </summary>
    /// <param name="value">Argument value to check.</param>
    /// <param name="name">Name of Argument.</param>
    public static void ThrowIfNull([NotNull] object? value, string name)
    {
        if (value == null)
        {
            throw new ArgumentNullException(name);
        }
    }

    /// <summary>
    /// Validates string and throws:
    /// ArgumentNullException if argument is null.
    /// ArgumentException if argument is empty.
    /// </summary>
    /// <param name="value">Argument value to check.</param>
    /// <param name="name">Name of Argument.</param>
    public static void ThrowIfNullOrEmpty([NotNull] string? value, string name)
    {
        if (value == null)
        {
            throw new ArgumentNullException(name);
        }

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("The value cannot be null or empty", name);
        }
    }
}
