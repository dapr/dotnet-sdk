// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Globalization;
using Microsoft.Extensions.Options;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Strategy that derives a numeric version from a zero-padded trailing suffix (for example, <c>MyWorkflow0001</c>).
/// </summary>
public sealed class ZeroPaddedNumericVersionStrategy(
    IOptionsMonitor<ZeroPaddedNumericVersionStrategyOptions>? optionsMonitor = null)
    : IWorkflowVersionStrategy, IWorkflowVersionStrategyContextConsumer
{
    private ZeroPaddedNumericVersionStrategyOptions _options = new();

    /// <inheritdoc />
    public void Configure(WorkflowVersionStrategyContext context)
    {
        var optionsName = string.IsNullOrWhiteSpace(context.OptionsName)
            ? Options.DefaultName
            : context.OptionsName;

        if (optionsMonitor is not null)
        {
            _options = optionsMonitor.Get(optionsName);
        }
    }

    /// <inheritdoc />
    public bool TryParse(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        var prefix = _options.SuffixPrefix ?? string.Empty;
        var comparison = _options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        var digitsStart = FindTrailingDigits(typeName);
        if (digitsStart < 0)
        {
            if (_options.AllowNoSuffix && !EndsWithPrefix(typeName, prefix, comparison))
            {
                canonicalName = typeName;
                version = string.IsNullOrWhiteSpace(_options.DefaultVersion) ? "0" : _options.DefaultVersion;
                return true;
            }

            return false;
        }

        var digitsLength = typeName.Length - digitsStart;
        if (_options.Width > 0 && digitsLength != _options.Width)
            return false;

        if (!string.IsNullOrEmpty(prefix))
        {
            var prefixStart = digitsStart - prefix.Length;
            if (prefixStart < 1)
                return false;

            var candidatePrefix = typeName.Substring(prefixStart, prefix.Length);
            if (!string.Equals(candidatePrefix, prefix, comparison))
                return false;

            canonicalName = typeName.Substring(0, prefixStart);
        }
        else
        {
            if (digitsStart < 1)
                return false;

            canonicalName = typeName.Substring(0, digitsStart);
        }

        version = typeName.Substring(digitsStart);
        return !string.IsNullOrEmpty(canonicalName) && !string.IsNullOrEmpty(version);
    }

    /// <inheritdoc />
    public int Compare(string? v1, string? v2)
    {
        if (ReferenceEquals(v1, v2)) return 0;
        if (v1 is null) return -1;
        if (v2 is null) return 1;

        var s1 = v1.Trim();
        var s2 = v2.Trim();

        var ok1 = long.TryParse(s1, NumberStyles.None, CultureInfo.InvariantCulture, out var n1);
        var ok2 = long.TryParse(s2, NumberStyles.None, CultureInfo.InvariantCulture, out var n2);

        switch (ok1)
        {
            case true when ok2:
                return n1.CompareTo(n2);
            case true:
                return 1;
        }

        if (ok2) return -1;

        return StringComparer.Ordinal.Compare(s1, s2);
    }

    private static int FindTrailingDigits(string value)
    {
        var i = value.Length - 1;
        while (i >= 0 && value[i] >= '0' && value[i] <= '9')
        {
            i--;
        }

        return i == value.Length - 1 ? -1 : i + 1;
    }

    private static bool EndsWithPrefix(string value, string prefix, StringComparison comparison) =>
        !string.IsNullOrEmpty(prefix) && value.EndsWith(prefix, comparison);
}
