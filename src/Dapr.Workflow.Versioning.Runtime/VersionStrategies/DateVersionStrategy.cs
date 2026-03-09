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
/// Strategy that derives a date-based version from a trailing suffix
/// (for example, <c>MyWorkflow20260212</c> with format <c>yyyyMMdd</c>).
/// </summary>
public sealed class DateVersionStrategy(IOptionsMonitor<DateVersionStrategyOptions>? optionsMonitor = null)
    : IWorkflowVersionStrategy, IWorkflowVersionStrategyContextConsumer
{
    private DateVersionStrategyOptions _options = new();

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

        var format = string.IsNullOrWhiteSpace(_options.DateFormat) ? "yyyyMMdd" : _options.DateFormat;
        var suffixLength = GetFormattedLength(format);
        var prefix = _options.Prefix ?? string.Empty;
        var comparison = _options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var totalSuffixLength = suffixLength + prefix.Length;
        if (typeName.Length <= totalSuffixLength)
            return ApplyNoSuffix(typeName, prefix, comparison, out canonicalName, out version);

        var suffixStart = typeName.Length - suffixLength;
        var dateSuffix = typeName.Substring(suffixStart, suffixLength);
        if (!TryParseDate(dateSuffix, format, _options.IgnorePrefixCase, out _))
            return ApplyNoSuffix(typeName, prefix, comparison, out canonicalName, out version);

        if (!string.IsNullOrEmpty(prefix))
        {
            var prefixStart = suffixStart - prefix.Length;
            if (prefixStart < 1)
                return false;

            var candidatePrefix = typeName.Substring(prefixStart, prefix.Length);
            if (!string.Equals(candidatePrefix, prefix, comparison))
                return false;

            canonicalName = typeName.Substring(0, prefixStart);
        }
        else
        {
            canonicalName = typeName.Substring(0, suffixStart);
        }

        if (string.IsNullOrEmpty(canonicalName))
            return false;

        version = dateSuffix;
        return true;
    }

    /// <inheritdoc />
    public int Compare(string? v1, string? v2)
    {
        if (ReferenceEquals(v1, v2)) return 0;
        if (v1 is null) return -1;
        if (v2 is null) return 1;

        var format = string.IsNullOrWhiteSpace(_options.DateFormat) ? "yyyyMMdd" : _options.DateFormat;
        var ok1 = TryParseDate(v1.Trim(), format, _options.IgnorePrefixCase, out var d1);
        var ok2 = TryParseDate(v2.Trim(), format, _options.IgnorePrefixCase, out var d2);

        switch (ok1)
        {
            case true when ok2:
                return d1.CompareTo(d2);
            case true:
                return 1;
        }

        if (ok2) return -1;

        return StringComparer.Ordinal.Compare(v1, v2);
    }

    private bool ApplyNoSuffix(
        string typeName,
        string prefix,
        StringComparison comparison,
        out string canonicalName,
        out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (!_options.AllowNoSuffix)
            return false;

        if (!string.IsNullOrEmpty(prefix) && typeName.EndsWith(prefix, comparison))
            return false;

        canonicalName = typeName;
        version = string.IsNullOrWhiteSpace(_options.DefaultVersion) ? "0" : _options.DefaultVersion;
        return true;
    }

    private static bool TryParseDate(string value, string format, bool ignorePrefixCase, out DateTime date)
    {
        if (DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        if (!ignorePrefixCase)
            return false;

        var upper = value.ToUpperInvariant();
        if (!string.Equals(upper, value, StringComparison.Ordinal) &&
            DateTime.TryParseExact(
                upper,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        var lower = value.ToLowerInvariant();
        if (!string.Equals(lower, value, StringComparison.Ordinal))
        {
            return DateTime.TryParseExact(
                lower,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        return false;
    }

    private static int GetFormattedLength(string format)
    {
        return DateTime.UnixEpoch.ToString(format, CultureInfo.InvariantCulture).Length;
    }
}
