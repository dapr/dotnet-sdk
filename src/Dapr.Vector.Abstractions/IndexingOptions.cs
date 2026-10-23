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

namespace Dapr.Vector;

/// <summary>Controls acknowledgement timing for vector upserts and deletion.</summary>
public sealed record IndexingOptions
{
    /// <summary>Defaults to returning when the provider accepts the write.</summary>
    public IndexingMode Mode { get; init; } = IndexingMode.ReturnOnAcceptance;

    /// <summary>
    /// Positive provider wait limit, required only for WaitForCompletion. Must be shorter than
    /// the RPC deadline and any store resiliency timeout (by at least 10% and at least 100ms).
    /// </summary>
    public TimeSpan? WaitTimeout { get; init; }

    /// <summary>Required only for WaitForCompletion.</summary>
    public IndexingWaitTimeoutAction? OnWaitTimeout { get; init; }
}

/// <summary>When a write should return.</summary>
public enum IndexingMode
{
    /// <summary>Same behavior as ReturnOnAcceptance.</summary>
    Unspecified = 0,

    /// <summary>Wait for a final provider result using an explicit timeout policy.</summary>
    WaitForCompletion = 1,

    /// <summary>Return after durable acceptance; no eventual result is exposed by this API.</summary>
    ReturnOnAcceptance = 2
}

/// <summary>Behavior when the provider wait limit expires.</summary>
public enum IndexingWaitTimeoutAction
{
    /// <summary>Return Queued and let work continue. Requires provider queued-ack support.</summary>
    ContinueAsync = 1,

    /// <summary>Fail with DeadlineExceeded. Provider-side work may still continue.</summary>
    FailRequest = 2
}
