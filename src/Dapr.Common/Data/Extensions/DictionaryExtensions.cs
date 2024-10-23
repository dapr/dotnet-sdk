// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Common.Data.Extensions;

/// <summary>
/// Provides extension methods for use with a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
internal static class DictionaryExtensions
{
    /// <summary>
    /// Merges the keys and values of the provided dictionary in mergeFrom with the
    /// dictionary provided in mergeTo.
    /// </summary>
    /// <param name="mergeTo">The dictionary the values are being merged into.</param>
    /// <param name="mergeFrom">The dictionary the values are being merged from.</param>
    /// <param name="prefix">The prefix to prepend to the key of the merged values.</param>
    /// <typeparam name="TValue">The type of the value for either dictionary.</typeparam>
    internal static void MergeFrom<TValue>(this Dictionary<string, TValue> mergeTo,
        Dictionary<string, TValue> mergeFrom, string prefix)
    {
        foreach (var kvp in mergeFrom)
        {
            var newKey = $"{prefix}{kvp.Key}";
            mergeTo[newKey] = kvp.Value;
        }
    }
}
