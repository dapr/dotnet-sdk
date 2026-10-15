namespace Dapr.Testcontainers.Xunit.Attributes;

internal static class DaprRuntimeVersionGate
{
    private const string RuntimeVersionEnvVarName = "DAPR_RUNTIME_VERSION";

    public static bool IsMinimumSatisfied(string minimumVersion, out string? reason)
    {
        if (!TryParseVersion(minimumVersion, out var minVersion))
            throw new ArgumentException(
                $"Invalid minimum Dapr runtime version '{minimumVersion}'.",
                nameof(minimumVersion));

        var currentRaw = Environment.GetEnvironmentVariable(RuntimeVersionEnvVarName);
        if (string.IsNullOrWhiteSpace(currentRaw) 
            || string.Equals(currentRaw, "latest", StringComparison.OrdinalIgnoreCase) 
            || !TryParseVersion(currentRaw, out var currentVersion) 
            || currentVersion >= minVersion)
        {
            reason = null;
            return true;
        }

        reason = $"Requires Dapr runtime >= {minimumVersion} (current: {currentRaw}).";
        return false;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[1..];

        var withoutMetadata = trimmed.Split('+', 2)[0];
        var withoutPrerelease = withoutMetadata.Split('-', 2)[0];
        var parts = withoutPrerelease.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var major))
            return false;
        if (!int.TryParse(parts[1], out var minor))
            return false;

        var patch = 0;
        if (parts.Length >= 3 && !int.TryParse(parts[2], out patch))
            return false;

        version = new Version(major, minor, patch);
        return true;
    }
}
