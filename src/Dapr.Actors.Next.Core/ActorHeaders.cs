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
    private const string ReentrancyHeaderName = "Dapr-Reentrancy-Id";
    private const string LegacyReentrancyHeaderName = "dapr-reentrant-id";
    private const string AlternateReentrancyHeaderName = "x-dapr-reentrant-id";
    internal const string ActivationIdHeaderName = "Dapr-Actor-Activation-Id";
    private static readonly string[] ReentrancyHeaderNames =
        [ReentrancyHeaderName, LegacyReentrancyHeaderName, AlternateReentrancyHeaderName];

    /// <summary>
    /// Gets an immutable empty header dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Empty { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Captures the current actor call chain in the canonical Dapr header without changing the input collection.
    /// Explicit call-chain headers take precedence over the current execution context.
    /// </summary>
    public static IReadOnlyDictionary<string, string> WithCurrentReentrancy(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (TryGetReentrancy(headers, out _, out var explicitValue))
        {
            return WithCanonicalReentrancy(headers, explicitValue);
        }

        var current = ActorTurnExecution.Current?.Headers;
        if (current is null || !TryGetReentrancy(current, out _, out var value))
        {
            return headers;
        }

        return WithCanonicalReentrancy(headers, value);
    }

    internal static IReadOnlyDictionary<string, string> EnsureReentrancy(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (TryGetReentrancy(headers, out _, out var value))
        {
            return WithCanonicalReentrancy(headers, value);
        }

        return WithCanonicalReentrancy(headers, Guid.NewGuid().ToString("N"));
    }

    internal static string GetActivationId(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (headers.TryGetValue(ActivationIdHeaderName, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        foreach (var (key, candidate) in headers)
        {
            if (!string.IsNullOrEmpty(candidate) && string.Equals(key, ActivationIdHeaderName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    internal static IReadOnlyDictionary<string, string> WithoutActivationId(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in headers)
        {
            if (!string.Equals(key, ActivationIdHeaderName, StringComparison.OrdinalIgnoreCase))
            {
                copy[key] = value;
            }
        }

        return copy;
    }

    internal static bool TryGetReentrancy(IReadOnlyDictionary<string, string> headers, out string key, out string value)
    {
        foreach (var name in ReentrancyHeaderNames)
        {
            foreach (var pair in headers)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    key = pair.Key;
                    value = pair.Value;
                    return true;
                }
            }
        }

        key = string.Empty;
        value = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<string, string> WithCanonicalReentrancy(IReadOnlyDictionary<string, string> headers, string value)
    {
        if (headers.TryGetValue(ReentrancyHeaderName, out var existing) && existing == value)
        {
            return headers;
        }

        return new Dictionary<string, string>(headers, StringComparer.Ordinal)
        {
            [ReentrancyHeaderName] = value,
        };
    }
}
