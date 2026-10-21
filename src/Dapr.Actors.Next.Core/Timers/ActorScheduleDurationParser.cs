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

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Dapr.Actors.Next.Core.Timers;

internal static partial class ActorScheduleDurationParser
{
    internal static TimeSpan? ParseOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Parse(value);
    }

    /// <summary>
    /// Parses an absolute RFC 3339 timestamp, as the sidecar reports timer and reminder expirations.
    /// </summary>
    internal static DateTimeOffset? ParseOptionalTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.Parse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    /// <summary>
    /// Parses a due time the sidecar echoes back as registered: either a duration (ISO 8601 or Go format)
    /// or an absolute RFC 3339 timestamp, which the HTTP API accepts. Exactly one result is set for a value.
    /// </summary>
    internal static (TimeSpan? Duration, DateTimeOffset? At) ParseOptionalDueTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        return IsTimestamp(value.Trim())
            ? (null, ParseOptionalTimestamp(value))
            : (Parse(value), null);
    }

    private static bool IsTimestamp(string value) => TimestampPrefixRegex().IsMatch(value);

    internal static TimeSpan Parse(string value)
    {
        var trimmed = value.Trim();
        const string EveryPrefix = "@every ";
        if (trimmed.StartsWith(EveryPrefix, StringComparison.Ordinal))
        {
            trimmed = trimmed[EveryPrefix.Length..].TrimStart();
        }

        if (trimmed.StartsWith('P') || trimmed.StartsWith("RP", StringComparison.Ordinal) || trimmed.StartsWith("R", StringComparison.Ordinal))
        {
            var duration = trimmed.StartsWith('R')
                ? trimmed[(trimmed.IndexOf('/') + 1)..]
                : trimmed;
            return XmlConvert.ToTimeSpan(duration);
        }

        var matches = DurationPartRegex().Matches(trimmed);
        if (matches.Count == 0)
        {
            return TimeSpan.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        var consumed = string.Concat(matches.Select(static match => match.Value));
        if (!string.Equals(consumed, trimmed, StringComparison.Ordinal))
        {
            return TimeSpan.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        var result = TimeSpan.Zero;
        foreach (Match match in matches)
        {
            var amount = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
            result += match.Groups["unit"].Value switch
            {
                "h" => TimeSpan.FromHours(amount),
                "m" => TimeSpan.FromMinutes(amount),
                "s" => TimeSpan.FromSeconds(amount),
                "ms" => TimeSpan.FromMilliseconds(amount),
                // Go formats microseconds as "µs" (U+00B5) and also parses "μs" (U+03BC) and "us".
                "us" or "\u00B5s" or "\u03BCs" => TimeSpan.FromTicks((long)(amount * 10)),
                "ns" => TimeSpan.FromTicks((long)(amount / 100)),
                _ => throw new FormatException($"Unsupported duration unit in '{value}'."),
            };
        }

        return result;
    }

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)(?<unit>ms|us|\u00B5s|\u03BCs|ns|h|m|s)", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPartRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampPrefixRegex();
}
