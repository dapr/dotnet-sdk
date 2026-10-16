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
    /// Gets or sets the payload boundaries where offloading is enabled.
    /// </summary>
    public PayloadKind OffloadSites { get; set; } = PayloadKind.All;

    /// <summary>
    /// Gets or sets the minimum serialized payload size, in bytes, required for offloading.
    /// A null value offloads every payload at an enabled boundary.
    /// </summary>
    public int? OffloadThreshold { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets whether an existing content-addressed file is treated as a successful write.
    /// </summary>
    public bool SkipWriteIfExists { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum payload size, in bytes, that can be buffered for automatic
    /// deserialization. Larger payloads must be consumed through a <see cref="PayloadReference{T}"/>.
    /// </summary>
    public long MaxInMemoryRehydrationSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether payload content is checked against its recorded SHA-256 hash after retrieval.
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
        ArgumentException.ThrowIfNullOrWhiteSpace(KeyPrefix);

        if (KeyPrefix.StartsWith('/') || KeyPrefix.EndsWith('/') ||
            KeyPrefix.Split('/').Any(segment => segment.Length == 0 || segment is "." or "..") ||
            KeyPrefix.IndexOfAny(['\\', '*', '?', '[', ']', '#']) >= 0)
        {
            throw new ArgumentException("KeyPrefix must be a valid binary store file name prefix.", nameof(KeyPrefix));
        }

        if (OffloadThreshold is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OffloadThreshold),
                "The offload threshold must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInMemoryRehydrationSize);
        ArgumentOutOfRangeException.ThrowIfNegative(ResolutionCacheSize);
    }
}
