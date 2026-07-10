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
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Strategy that derives a numeric version from a trailing suffix with an optional prefix.
/// </summary>
public sealed class NumericVersionStrategy : IActorStateVersionStrategy, IActorStateVersionStrategyContextConsumer
{
    private readonly IOptionsMonitor<NumericVersionStrategyOptions>? optionsMonitor;
    private NumericVersionStrategyOptions options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NumericVersionStrategy"/> class.
    /// </summary>
    public NumericVersionStrategy(IOptionsMonitor<NumericVersionStrategyOptions>? optionsMonitor = null)
    {
        this.optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public void Configure(ActorStateVersionStrategyContext context)
    {
        var optionsName = string.IsNullOrWhiteSpace(context.OptionsName)
            ? Microsoft.Extensions.Options.Options.DefaultName
            : context.OptionsName;

        if (optionsMonitor is not null)
        {
            options = optionsMonitor.Get(optionsName);
        }
    }

    /// <inheritdoc />
    public bool TryParse(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        var prefix = options.SuffixPrefix ?? string.Empty;
        var comparison = options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        var digitsStart = FindTrailingDigits(typeName);
        if (digitsStart < 0)
        {
            if (options.AllowNoSuffix && !EndsWithPrefix(typeName, prefix, comparison))
            {
                canonicalName = typeName;
                version = string.IsNullOrWhiteSpace(options.DefaultVersion) ? "0" : options.DefaultVersion;
                return true;
            }

            return false;
        }

        var digitsLength = typeName.Length - digitsStart;
        if (options.ZeroPad && options.Width > 0 && digitsLength != options.Width)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            var prefixStart = digitsStart - prefix.Length;
            if (prefixStart < 1)
            {
                return false;
            }

            var candidatePrefix = typeName.Substring(prefixStart, prefix.Length);
            if (!string.Equals(candidatePrefix, prefix, comparison))
            {
                return false;
            }

            canonicalName = typeName[..prefixStart];
        }
        else
        {
            if (digitsStart < 1)
            {
                return false;
            }

            canonicalName = typeName[..digitsStart];
        }

        version = typeName[digitsStart..];
        return !string.IsNullOrEmpty(canonicalName) && !string.IsNullOrEmpty(version);
    }

    /// <inheritdoc />
    public int Compare(string? v1, string? v2)
    {
        if (ReferenceEquals(v1, v2))
        {
            return 0;
        }

        if (v1 is null)
        {
            return -1;
        }

        if (v2 is null)
        {
            return 1;
        }

        var ok1 = long.TryParse(v1.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n1);
        var ok2 = long.TryParse(v2.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n2);

        if (ok1 && ok2)
        {
            return n1.CompareTo(n2);
        }

        if (ok1)
        {
            return 1;
        }

        if (ok2)
        {
            return -1;
        }

        return StringComparer.Ordinal.Compare(v1, v2);
    }

    private static int FindTrailingDigits(string value)
    {
        var i = value.Length - 1;
        while (i >= 0 && value[i] is >= '0' and <= '9')
        {
            i--;
        }

        return i == value.Length - 1 ? -1 : i + 1;
    }

    private static bool EndsWithPrefix(string value, string prefix, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        return value.EndsWith(prefix, comparison);
    }
}
