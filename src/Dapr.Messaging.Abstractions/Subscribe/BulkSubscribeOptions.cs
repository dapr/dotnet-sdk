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

namespace Dapr.Messaging;

/// <summary>
/// Bulk subscribe configuration for a topic subscription.
/// </summary>
public sealed class BulkSubscribeOptions
{
    /// <summary>
    /// Whether bulk subscribe is enabled for the subscription.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// The maximum number of messages delivered in a single bulk request.
    /// </summary>
    public int MaxMessagesCount { get; init; } = 100;

    /// <summary>
    /// The maximum time, in milliseconds, the runtime waits to assemble a bulk request before delivering it.
    /// </summary>
    public int MaxAwaitDurationMs { get; init; } = 1000;
}
