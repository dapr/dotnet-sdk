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

using Microsoft.Extensions.Options;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Strategy that derives a SemVer version from a trailing suffix (for example, <c>MyWorkflowv1.2.3</c>).
/// </summary>
public sealed class SemVerVersionStrategy(IOptionsMonitor<SemVerVersionStrategyOptions>? optionsMonitor = null)
    : IWorkflowVersionStrategy, IWorkflowVersionStrategyContextConsumer
{
    private SemVerVersionStrategyOptions _options = new();

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

        var prefix = _options.Prefix ?? string.Empty;
        if (prefix.Length > 0)
        {
            var comparison = _options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var prefixIndex = typeName.LastIndexOf(prefix, comparison);
            if (prefixIndex < 0)
            {
                return ApplyNoSuffix(typeName, out canonicalName, out version);
            }

            var versionStart = prefixIndex + prefix.Length;
            if (prefixIndex == 0 || versionStart >= typeName.Length)
                return false;

            var candidate = typeName.Substring(versionStart);
            if (!TryParseSemVer(candidate, _options, out _))
                return false;

            canonicalName = typeName.Substring(0, prefixIndex);
            version = candidate;
            return !string.IsNullOrEmpty(canonicalName);
        }

        var candidateStart = FindSemVerSuffixStart(typeName);
        if (candidateStart < 0)
            return ApplyNoSuffix(typeName, out canonicalName, out version);

        var suffix = typeName.Substring(candidateStart);
        if (!TryParseSemVer(suffix, _options, out _))
            return ApplyNoSuffix(typeName, out canonicalName, out version);

        canonicalName = typeName.Substring(0, candidateStart);
        if (string.IsNullOrEmpty(canonicalName))
            return false;

        version = suffix;
        return true;
    }

    /// <inheritdoc />
    public int Compare(string? v1, string? v2)
    {
        if (ReferenceEquals(v1, v2)) return 0;
        if (v1 is null) return -1;
        if (v2 is null) return 1;

        var ok1 = TryParseSemVer(v1.Trim(), _options, out var s1);
        var ok2 = TryParseSemVer(v2.Trim(), _options, out var s2);

        switch (ok1)
        {
            case true when ok2:
                return s1.CompareTo(s2);
            case true:
                return 1;
        }

        if (ok2) return -1;

        return StringComparer.Ordinal.Compare(v1, v2);
    }

    private bool ApplyNoSuffix(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (!_options.AllowNoSuffix)
            return false;

        canonicalName = typeName;
        version = string.IsNullOrWhiteSpace(_options.DefaultVersion) ? "0.0.0" : _options.DefaultVersion;
        return true;
    }

    private static int FindSemVerSuffixStart(string value)
    {
        var i = value.Length - 1;
        while (i >= 0 && IsSemVerChar(value[i]))
        {
            i--;
        }

        return i == value.Length - 1 ? -1 : i + 1;
    }

    private static bool IsSemVerChar(char c) =>
        c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '.' or '-' or '+';

    private static bool TryParseSemVer(string value, SemVerVersionStrategyOptions options, out SemVer semVer)
    {
        semVer = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var buildSplit = value.Split('+', 2);
        if (buildSplit.Length == 2 && !options.AllowBuildMetadata)
            return false;

        var withoutBuild = buildSplit[0];
        var preSplit = withoutBuild.Split('-', 2);
        var core = preSplit[0];

        if (preSplit.Length == 2 && !options.AllowPrerelease)
            return false;

        var coreParts = core.Split('.');
        if (coreParts.Length != 3)
            return false;

        if (!int.TryParse(coreParts[0], out var major) ||
            !int.TryParse(coreParts[1], out var minor) ||
            !int.TryParse(coreParts[2], out var patch))
        {
            return false;
        }

        var prerelease = preSplit.Length == 2 ? preSplit[1] : null;
        var build = buildSplit.Length == 2 ? buildSplit[1] : null;

        semVer = new SemVer(major, minor, patch, prerelease, build);
        return true;
    }

    private readonly struct SemVer : IComparable<SemVer>
    {
        public SemVer(int major, int minor, int patch, string? prerelease, string? build)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease;
            Build = build;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? Prerelease { get; }
        public string? Build { get; }

        public int CompareTo(SemVer other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;

            var patch = Patch.CompareTo(other.Patch);
            if (patch != 0) return patch;

            var thisPre = Prerelease;
            var otherPre = other.Prerelease;

            if (string.IsNullOrEmpty(thisPre) && string.IsNullOrEmpty(otherPre)) return 0;
            if (string.IsNullOrEmpty(thisPre)) return 1;
            if (string.IsNullOrEmpty(otherPre)) return -1;

            return ComparePrerelease(thisPre, otherPre);
        }

        private static int ComparePrerelease(string left, string right)
        {
            var leftParts = left.Split('.');
            var rightParts = right.Split('.');
            var length = Math.Max(leftParts.Length, rightParts.Length);

            for (var i = 0; i < length; i++)
            {
                if (i >= leftParts.Length) return -1;
                if (i >= rightParts.Length) return 1;

                var l = leftParts[i];
                var r = rightParts[i];

                var lIsNum = int.TryParse(l, out var lNum);
                var rIsNum = int.TryParse(r, out var rNum);

                switch (lIsNum)
                {
                    case true when rIsNum:
                    {
                        var cmp = lNum.CompareTo(rNum);
                        if (cmp != 0) return cmp;
                        continue;
                    }
                    case true:
                        return -1;
                }

                if (rIsNum) return 1;

                var cmpStr = StringComparer.Ordinal.Compare(l, r);
                if (cmpStr != 0) return cmpStr;
            }

            return 0;
        }
    }
}
