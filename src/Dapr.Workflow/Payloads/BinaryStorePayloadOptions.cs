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

using System;
using System.Linq;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Configures offloading and resolution of Dapr Workflow payloads in a Dapr binary store.
/// </summary>
public sealed class BinaryStorePayloadOptions
{
    /// <summary>
    /// Gets or sets the name of the Dapr binary store component.
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key prefix used for stored payloads.
    /// </summary>
    public string KeyPrefix { get; set; } = "workflow-payloads";

    /// <summary>
    /// Gets or sets an optional factory for a relative prefix beneath <see cref="KeyPrefix"/>,
    /// evaluated for each new payload write using its owning workflow instance ID.
    /// </summary>
    /// <remarks>
    /// For example, returning <c>tenants/acme/instances</c> produces
    /// <c>{KeyPrefix}/tenants/acme/instances/{escapedInstanceId}/{payloadKey}</c>.
    /// The SDK always appends the instance ID and controls the payload key.
    /// The factory must return a non-empty relative prefix without empty, dot, or parent segments.
    /// It must be thread-safe and stable across participating clients, workers, and retries;
    /// do not depend on ambient request state. Exceptions and invalid prefixes fail the write.
    /// Existing references are read and deleted using their recorded keys, without invoking the factory.
    /// Prefixes organize data; they do not enforce tenant authorization or propagate tenant metadata.
    /// </remarks>
    public Func<string, string>? InstancePrefixFactory { get; set; }

    /// <summary>
    /// Gets or sets the conversion preference for future writes. Defaults to
    /// <see cref="PayloadWritePreference.Preserve"/>, which performs no automatic conversion.
    /// </summary>
    /// <remarks>
    /// Existing references remain readable. Explicit calls to
    /// <see cref="IWorkflowPayloadResolver.StoreAsync{T}"/> and
    /// <see cref="IWorkflowPayloadResolver.StoreStreamAsync"/> and <see cref="IWorkflowPayloadResolver.WriteAsync"/>
    /// still store payloads under every preference, as do all <c>UploadAsync</c> overloads.
    /// This host-wide option affects new writes for both new and running workflows; it does not
    /// rewrite history. Use a new workflow version when changing explicit reference contracts
    /// to ordinary payload types, and retain the old version for existing instances.
    /// </remarks>
    public PayloadWritePreference WritePreference { get; set; } = PayloadWritePreference.Preserve;

    /// <summary>
    /// Gets or sets the payload boundaries where <see cref="PayloadWritePreference.PreferReference"/>
    /// may automatically create references. Does not restrict reference reads or inlining.
    /// </summary>
    public PayloadKind OffloadSites { get; set; } = PayloadKind.All;

    /// <summary>
    /// Gets or sets the minimum serialized payload size, in bytes, required for offloading.
    /// A null value offloads every payload at an enabled boundary.
    /// Stream inputs to <see cref="DaprWorkflowClient.ScheduleNewWorkflowAsync(string, string, object)"/>
    /// always become references, regardless of this threshold, when
    /// <see cref="PayloadWritePreference.PreferReference"/> and <see cref="PayloadKind.WorkflowInput"/> are enabled.
    /// </summary>
    public int? OffloadThreshold { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets whether an existing content-addressed file is treated as a successful write.
    /// </summary>
    public bool SkipWriteIfExists { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum payload size, in bytes, that can be buffered for automatic
    /// deserialization or outbound inlining. Larger payloads must be consumed through a
    /// <see cref="PayloadReference{T}"/>. This is not a transport message-size limit.
    /// </summary>
    public long MaxInMemoryRehydrationSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether materialized payload reads are checked against their recorded SHA-256 hash.
    /// Raw stream and chunk reads do not verify integrity, including when this option is enabled.
    /// </summary>
    public bool VerifyIntegrityOnRead { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of payload resolutions cached for one orchestrator execution.
    /// Set to zero to disable caching.
    /// </summary>
    public int ResolutionCacheSize { get; set; } = 128;

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <exception cref="ArgumentException">A required name or prefix is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric option is outside its valid range.</exception>
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(StoreName);
        ValidatePrefix(KeyPrefix, nameof(KeyPrefix));
        if (!Enum.IsDefined(WritePreference))
        {
            throw new ArgumentOutOfRangeException(nameof(WritePreference));
        }

        if (OffloadThreshold is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OffloadThreshold),
                "The offload threshold must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInMemoryRehydrationSize);
        ArgumentOutOfRangeException.ThrowIfNegative(ResolutionCacheSize);
    }

    internal string GetInstancePrefix(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var prefix = KeyPrefix;
        if (InstancePrefixFactory is { } factory)
        {
            var relativePrefix = factory(instanceId);
            ValidatePrefix(relativePrefix, nameof(InstancePrefixFactory));
            prefix = $"{prefix}/{relativePrefix}";
        }

        // Keep the entire ID in a single segment, including IDs containing slashes or percent signs.
        var segment = instanceId is "." or ".."
            ? instanceId.Replace(".", "%2E", StringComparison.Ordinal)
            : Uri.EscapeDataString(instanceId);
        return $"{prefix}/{segment}";
    }

    private static void ValidatePrefix(string prefix, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix, parameterName);
        if (prefix.StartsWith('/') || prefix.EndsWith('/') ||
            prefix.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or "..") ||
            prefix.IndexOfAny(['\\', ':', '*', '?', '[', ']', '#', '%']) >= 0 || prefix.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The prefix must be a relative binary store path without empty, dot, parent, encoded, or reserved segments.",
                parameterName);
        }
    }
}
