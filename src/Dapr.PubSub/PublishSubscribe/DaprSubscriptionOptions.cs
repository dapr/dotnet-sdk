// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.PubSub.PublishSubscribe;

/// <summary>
/// Options used to configure the dynamic Dapr subscription.
/// </summary>
/// <param name="MessageHandlingPolicy">Describes the policy to take on messages that have not been acknowledged within the timeout period.</param>
public sealed record DaprSubscriptionOptions(MessageHandlingPolicy MessageHandlingPolicy)
{
    /// <summary>
    /// Subscription metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    
    /// <summary>
    /// The optional name of the dead-letter topic to send unprocessed messages to.
    /// </summary>
    public string? DeadLetterTopic { get; init; }

    /// <summary>
    /// If populated, this reflects the maximum number of messages that can be queued for processing on the replica. By default,
    /// no maximum boundary is enforced.
    /// </summary>
    public int? MaximumQueuedMessages { get; init; }

    /// <summary>
    /// The maximum amount of time to take to dispose of acknowledgement messages after the cancellation token has
    /// been signaled.
    /// </summary>
    public TimeSpan MaximumCleanupTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

