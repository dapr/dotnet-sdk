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
/// Contract implemented by the assembly-level subscriber registry emitted by the
/// <c>Dapr.Messaging.Generators</c> source generator. Implemented by generated code only.
/// </summary>
/// <remarks>
/// Public (rather than internal) because the generated registry lives in the consumer's assembly,
/// which cannot be granted <c>InternalsVisibleTo</c> at Abstractions build time.
/// </remarks>
public interface IDaprMessagingSubscriberRegistry
{
    /// <summary>The complete set of subscription descriptors discovered at compile time.</summary>
    IReadOnlyList<TopicSubscriptionDescriptor> Descriptors { get; }

    /// <summary>
    /// Resolves the dispatcher for a given <paramref name="pubsubName"/>/<paramref name="topicName"/>
    /// delivered via the specified <paramref name="mode"/>.
    /// </summary>
    /// <returns>The matching dispatcher, or <c>null</c> if no handler is registered.</returns>
    ITopicDispatcher? Resolve(string pubsubName, string topicName, DeliveryMode mode);
}
