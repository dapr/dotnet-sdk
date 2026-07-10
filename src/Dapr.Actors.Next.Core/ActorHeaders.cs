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

using System.Collections.ObjectModel;
using Dapr.Actors.Next.Core.Runtime;

namespace Dapr.Actors.Next.Core;

/// <summary>
/// Shared actor header collections for allocation-sensitive runtime paths.
/// </summary>
public static class ActorHeaders
{
    private const string ReentrancyHeaderName = "dapr-reentrant-id";
    private const string AlternateReentrancyHeaderName = "x-dapr-reentrant-id";

    /// <summary>
    /// Gets an immutable empty header dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Empty { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    internal static IReadOnlyDictionary<string, string> WithCurrentReentrancy(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (TryGetReentrancy(headers, out _, out _))
        {
            return headers;
        }

        var current = ActorTurnExecution.Current?.Headers;
        if (current is null || !TryGetReentrancy(current, out var key, out var value))
        {
            return headers;
        }

        if (headers.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = value,
            });
        }

        var merged = new Dictionary<string, string>(headers, StringComparer.Ordinal)
        {
            [key] = value,
        };
        return merged;
    }

    internal static IReadOnlyDictionary<string, string> EnsureReentrancy(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (TryGetReentrancy(headers, out _, out _))
        {
            return headers;
        }

        var copy = headers.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);
        copy[ReentrancyHeaderName] = Guid.NewGuid().ToString("N");
        return copy;
    }

    internal static bool TryGetReentrancy(IReadOnlyDictionary<string, string> headers, out string key, out string value)
    {
        if (headers.TryGetValue(ReentrancyHeaderName, out value!) && !string.IsNullOrWhiteSpace(value))
        {
            key = ReentrancyHeaderName;
            return true;
        }

        if (headers.TryGetValue(AlternateReentrancyHeaderName, out value!) && !string.IsNullOrWhiteSpace(value))
        {
            key = AlternateReentrancyHeaderName;
            return true;
        }

        key = string.Empty;
        value = string.Empty;
        return false;
    }
}
