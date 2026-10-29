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

namespace Dapr.Common;

/// <summary>
/// Trusts the actor activation id only when the sidecar is new enough to drop caller-supplied copies of it.
/// </summary>
internal sealed class ActorActivationIdGate
{
    private static readonly TimeSpan DefaultLookupTimeout = TimeSpan.FromSeconds(5);
    private static readonly Version MinimumRuntimeVersion = new(1, 19, 0);

    private readonly Func<CancellationToken, Task<string?>> getRuntimeVersion;
    private readonly TimeSpan lookupTimeout;
    private readonly object sync = new();
    private Task<bool>? lookup;

    /// <summary>
    /// Creates a gate that reads the sidecar runtime version with <paramref name="getRuntimeVersion"/>.
    /// </summary>
    public ActorActivationIdGate(Func<CancellationToken, Task<string?>> getRuntimeVersion, TimeSpan? lookupTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(getRuntimeVersion);
        this.getRuntimeVersion = getRuntimeVersion;
        this.lookupTimeout = lookupTimeout ?? DefaultLookupTimeout;
    }

    /// <summary>
    /// Returns the activation id when the sidecar is trusted, otherwise null.
    /// </summary>
    public async Task<string?> FilterAsync(string? activationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activationId))
        {
            return activationId;
        }

        return await IsTrustedAsync(cancellationToken).ConfigureAwait(false) ? activationId : null;
    }

    internal static bool IsSupported(string? runtimeVersion)
    {
        if (string.IsNullOrWhiteSpace(runtimeVersion))
        {
            return false;
        }

        var core = runtimeVersion.Trim();
        var build = core.IndexOf('+');
        if (build >= 0)
        {
            core = core[..build];
        }

        var prerelease = core.IndexOf('-');
        var isPrerelease = prerelease >= 0;
        if (isPrerelease)
        {
            core = core[..prerelease];
        }

        var parts = core.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        // Release candidates of the minimum version came before the activation id shipped.
        var version = new Version(major, minor, patch);
        return version > MinimumRuntimeVersion || (version == MinimumRuntimeVersion && !isPrerelease);
    }

    private async Task<bool> IsTrustedAsync(CancellationToken cancellationToken)
    {
        Task<bool> current;
        lock (sync)
        {
            current = lookup ??= LookupAsync();
        }

        try
        {
            return await current.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            lock (sync)
            {
                if (ReferenceEquals(lookup, current))
                {
                    lookup = null;
                }
            }

            return false;
        }
    }

    private async Task<bool> LookupAsync()
    {
        await Task.Yield();
        using var timeout = new CancellationTokenSource(lookupTimeout);
        var version = await getRuntimeVersion(timeout.Token).WaitAsync(lookupTimeout).ConfigureAwait(false);
        return IsSupported(version);
    }
}
