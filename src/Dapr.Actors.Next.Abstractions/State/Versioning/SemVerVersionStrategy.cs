using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Strategy that derives a SemVer version from a trailing suffix.
/// </summary>
public sealed class SemVerVersionStrategy(IOptionsMonitor<SemVerVersionStrategyOptions>? optionsMonitor = null)
    : IActorStateVersionStrategy, IActorStateVersionStrategyContextConsumer
{
    private SemVerVersionStrategyOptions options = new();

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

        var prefix = options.Prefix ?? string.Empty;
        if (prefix.Length > 0)
        {
            var comparison = options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var prefixIndex = typeName.LastIndexOf(prefix, comparison);
            if (prefixIndex < 0)
            {
                return ApplyNoSuffix(typeName, out canonicalName, out version);
            }

            var versionStart = prefixIndex + prefix.Length;
            if (prefixIndex == 0 || versionStart >= typeName.Length)
            {
                return false;
            }

            var candidate = typeName[versionStart..];
            if (!TryParseSemVer(candidate, options, out _))
            {
                return false;
            }

            canonicalName = typeName[..prefixIndex];
            version = candidate;
            return !string.IsNullOrEmpty(canonicalName);
        }

        var candidateStart = FindSemVerSuffixStart(typeName);
        if (candidateStart < 0)
        {
            return ApplyNoSuffix(typeName, out canonicalName, out version);
        }

        var suffix = typeName[candidateStart..];
        if (!TryParseSemVer(suffix, options, out _))
        {
            return ApplyNoSuffix(typeName, out canonicalName, out version);
        }

        canonicalName = typeName[..candidateStart];
        if (string.IsNullOrEmpty(canonicalName))
        {
            return false;
        }

        version = suffix;
        return true;
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

        var ok1 = TryParseSemVer(v1.Trim(), options, out var s1);
        var ok2 = TryParseSemVer(v2.Trim(), options, out var s2);

        if (ok1 && ok2)
        {
            return s1.CompareTo(s2);
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

    private bool ApplyNoSuffix(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (!options.AllowNoSuffix)
        {
            return false;
        }

        canonicalName = typeName;
        version = string.IsNullOrWhiteSpace(options.DefaultVersion) ? "0.0.0" : options.DefaultVersion;
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
        {
            return false;
        }

        var buildSplit = value.Split('+', 2);
        if (buildSplit.Length == 2 && !options.AllowBuildMetadata)
        {
            return false;
        }

        var withoutBuild = buildSplit[0];
        var preSplit = withoutBuild.Split('-', 2);
        var core = preSplit[0];

        if (preSplit.Length == 2 && !options.AllowPrerelease)
        {
            return false;
        }

        var parts = core.Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            return false;
        }

        var prerelease = preSplit.Length == 2 ? preSplit[1] : null;
        var build = buildSplit.Length == 2 ? buildSplit[1] : null;

        semVer = new SemVer(major, minor, patch, prerelease, build);
        return true;
    }

    private readonly record struct SemVer(int Major, int Minor, int Patch, string? Prerelease, string? Build) : IComparable<SemVer>
    {
        public int CompareTo(SemVer other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0)
            {
                return major;
            }

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0)
            {
                return minor;
            }

            var patch = Patch.CompareTo(other.Patch);
            if (patch != 0)
            {
                return patch;
            }

            var thisPre = Prerelease;
            var otherPre = other.Prerelease;

            if (string.IsNullOrEmpty(thisPre) && string.IsNullOrEmpty(otherPre))
            {
                return 0;
            }

            if (string.IsNullOrEmpty(thisPre))
            {
                return 1;
            }

            if (string.IsNullOrEmpty(otherPre))
            {
                return -1;
            }

            return ComparePrerelease(thisPre, otherPre);
        }

        private static int ComparePrerelease(string left, string right)
        {
            var leftParts = left.Split('.');
            var rightParts = right.Split('.');
            var length = Math.Max(leftParts.Length, rightParts.Length);

            for (var i = 0; i < length; i++)
            {
                if (i >= leftParts.Length)
                {
                    return -1;
                }

                if (i >= rightParts.Length)
                {
                    return 1;
                }

                var l = leftParts[i];
                var r = rightParts[i];

                var lIsNum = int.TryParse(l, out var lNum);
                var rIsNum = int.TryParse(r, out var rNum);

                switch (lIsNum)
                {
                    case true when rIsNum:
                    {
                        var cmp = lNum.CompareTo(rNum);
                        if (cmp != 0)
                        {
                            return cmp;
                        }

                        continue;
                    }

                    case true:
                        return -1;
                }

                if (rIsNum)
                {
                    return 1;
                }

                var cmpStr = StringComparer.Ordinal.Compare(l, r);
                if (cmpStr != 0)
                {
                    return cmpStr;
                }
            }

            return 0;
        }
    }
}
