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
                "us" => TimeSpan.FromTicks((long)(amount * 10)),
                "ns" => TimeSpan.FromTicks((long)(amount / 100)),
                _ => throw new FormatException($"Unsupported duration unit in '{value}'."),
            };
        }

        return result;
    }

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)(?<unit>ms|us|ns|h|m|s)", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPartRegex();
}
